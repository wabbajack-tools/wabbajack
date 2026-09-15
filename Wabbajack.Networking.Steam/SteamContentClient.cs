using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SteamKit2;
using SteamKit2.CDN;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.Steam.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Reads content out of Steam: product info, entitlement, depot keys, manifests and files. Everything
///     about being logged in lives in <see cref="SteamSession" />.
///     The sequence a single file takes, which is Valve's and not negotiable at any step:
///     <list type="number">
///         <item>PICS access token, then product info, for the app.</item>
///         <item>Entitlement, from the account's licences, falling back to the app being free to download.</item>
///         <item>The depot decryption key. Without it nothing that comes back can be read.</item>
///         <item>The SteamPipe server directory, filtered to servers that will serve this app.</item>
///         <item>
///             A manifest request code. Mandatory since 2022 and short lived; a zero back is refusal, not a
///             transient failure.
///         </item>
///         <item>The manifest from a content server, with a CDN auth token if that server wants one.</item>
///         <item>The wanted file's chunks, decrypted and written at their offsets, then hash-checked.</item>
///     </list>
/// </summary>
public class SteamContentClient : ISteamContentClient, IDisposable
{
    /// <summary>The branch a public build lives on. Everything here is public-branch content.</summary>
    public const string PublicBranch = "public";

    private const int MaxServerAttempts = 6;

    /// <summary>
    ///     How many manifests' file lists are kept at once. A version resolves to a handful of depots and a
    ///     repair searches all of them, so this holds a whole repair; anything beyond that is paid for in
    ///     memory that is never handed back.
    /// </summary>
    private const int MaxCachedManifests = 4;

    /// <summary>
    ///     Licences arrive on their own schedule after logon. Long enough that a slow connection is not cut
    ///     off, short enough that a Steam that is never going to answer does not hold up the run.
    /// </summary>
    private static readonly TimeSpan LicenseWait = TimeSpan.FromSeconds(30);

    private readonly DepotFileAssembler _assembler;
    private readonly ConcurrentDictionary<(uint DepotId, string Host), string> _cdnAuthTokens = new();
    private readonly ManifestRequestCodeCache _codes = new();

    /// <summary>
    ///     The file lists of manifests already downloaded and decrypted, so a repair that reads the same
    ///     build several times pays for it once. Bounded, because this object lives as long as its host and
    ///     a single manifest of a large game is tens of megabytes of <see cref="DepotManifest.FileData" />.
    ///     <para>
    ///         Nothing in here leaves this class. The list is a read-only view over a private array, so
    ///         several callers reading the same manifest cannot sort, filter or add to what the others are
    ///         holding, and everything public returns <see cref="DepotFile" /> records built from it.
    ///     </para>
    /// </summary>
    private readonly ManifestCache<IReadOnlyList<DepotManifest.FileData>> _manifestFiles =
        new(MaxCachedManifests);

    private readonly DTOSerializer _dtos;
    private readonly ILogger<SteamContentClient> _logger;
    private readonly SteamSession _session;

    private readonly object _poolLock = new();

    private Client? _cdn;
    private bool _disposed;
    private CdnServerPool<Server>? _pool;

    public SteamContentClient(ILogger<SteamContentClient> logger, SteamSession session, DTOSerializer dtos,
        IResource<HttpClient> limiter)
    {
        _logger = logger;
        _session = session;
        _dtos = dtos;
        _assembler = new DepotFileAssembler(logger, limiter);
    }

    public ConcurrentDictionary<uint, ulong> PackageTokens { get; } = new();

    public ConcurrentDictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo?> PackageInfos { get; } = new();

    public ConcurrentDictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo> AppProductInfos { get; } =
        new();

    public ConcurrentDictionary<uint, ulong> AppTokens { get; } = new();

    public ConcurrentDictionary<uint, byte[]> DepotKeys { get; } = new();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cdn?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo?>> GetPackageInfos(
        IEnumerable<uint> packageIds)
    {
        var requested = packageIds.Distinct().ToList();
        var missing = requested.Where(id => !PackageInfos.ContainsKey(id)).ToList();

        if (missing.Count > 0)
        {
            var packageRequests = new List<SteamApps.PICSRequest>();

            foreach (var package in missing)
            {
                var request = new SteamApps.PICSRequest(package);
                if (PackageTokens.TryGetValue(package, out var token)) request.AccessToken = token;

                packageRequests.Add(request);
            }

            _logger.LogInformation("Requesting {Count} package infos", packageRequests.Count);

            var results = await _session.Apps.PICSGetProductInfo(new List<SteamApps.PICSRequest>(), packageRequests);

            if (results.Failed || results.Results == null)
                throw new SteamException("Exception getting product info", EResult.Invalid, EResult.Invalid);

            foreach (var packageInfo in results.Results)
            {
                foreach (var package in packageInfo.Packages.Select(v => v.Value)) PackageInfos[package.ID] = package;

                foreach (var package in packageInfo.UnknownPackages) PackageInfos[package] = null;
            }

            // Steam answers about what it knows. A package it said nothing about is not going to turn up
            // later in this run, and leaving it out of the dictionary would fault every caller that indexes
            // the result.
            foreach (var package in missing.Where(p => !PackageInfos.ContainsKey(p))) PackageInfos[package] = null;
        }

        return requested.ToDictionary(p => p, p => PackageInfos[p]);
    }

    /// <summary>
    ///     The app's product info, as the typed view of it. Kept separate from
    ///     <see cref="GetAppProductInfo" /> on purpose: translating the whole PICS tree into a DTO is only
    ///     worth doing for a caller that wants the depot layout, and nothing that merely needs one key from
    ///     it should be able to fail because some unrelated corner of Steam's data did not fit.
    /// </summary>
    public async Task<AppInfo> GetAppInfo(uint appId)
    {
        var info = await GetAppProductInfo(appId);
        return KeyValueTranslator.Translate<AppInfo>(info.KeyValues, _dtos);
    }

    /// <summary>
    ///     The manifest a depot is publishing on a branch right now.
    ///     Steam only ever exposes the current one -- there is no way to enumerate history through the
    ///     client API, which is the whole reason Wabbajack keeps its own version-to-manifest index. So this
    ///     answers the "the user never installed it" case and nothing else; a file from an older game
    ///     version has to come from a manifest id somebody recorded at the time.
    /// </summary>
    public async Task<ulong?> GetCurrentManifestIdAsync(uint appId, uint depotId, string branch = PublicBranch)
    {
        var app = await GetAppInfo(appId);

        return app.GetDepots(_dtos.Options)
            .Where(d => d.DepotId == depotId)
            .Select(d => d.Depot.ManifestFor(branch))
            .FirstOrDefault();
    }

    /// <summary>
    ///     Every depot of the app publishing on the branch today, with what it publishes.
    ///     Two kinds of entry are left out because fetching from them would be a mistake rather than a
    ///     miss: a depot whose content actually belongs to another app (<c>depotfromapp</c>), which has to
    ///     be asked for under that app's id, and one whose <c>oslist</c> says it is for another operating
    ///     system. A depot that names no oslist is kept - most do not, and the games this serves are
    ///     Windows games.
    /// </summary>
    public async Task<IReadOnlyList<DepotManifestId>> GetCurrentDepotsAsync(uint appId,
        string branch = PublicBranch)
    {
        var app = await GetAppInfo(appId);

        return app.GetDepots(_dtos.Options)
            .Where(d => d.Depot.DepotFromApp == 0)
            .Where(d => d.Depot.Config?.OSList is not {Length: > 0} os ||
                        os.Contains("windows", StringComparison.OrdinalIgnoreCase))
            .Select(d => (d.DepotId, Manifest: d.Depot.ManifestFor(branch)))
            .Where(d => d.Manifest.HasValue)
            .Select(d => new DepotManifestId(d.DepotId, d.Manifest!.Value))
            .ToArray();
    }

    /// <summary>
    ///     Whether the logged in account may open this depot: a licence naming it, or an app that is free to
    ///     download. Asked before anything is fetched, because Steam's own answer to an unentitled request is
    ///     a refused decryption key several calls further on.
    /// </summary>
    public async Task<DepotAccess> CheckAccessAsync(uint appId, uint depotId, CancellationToken token)
    {
        EnsureLoggedIn();

        var (held, licensesArrived) = await HoldsLicenseForAsync(depotId, token).ConfigureAwait(false);
        if (held) return DepotAccess.Granted;

        var app = await GetAppProductInfo(appId);
        if (DepotEntitlement.IsFreeToDownload(app.KeyValues))
        {
            _logger.LogInformation("App {AppId} is free to download, so depot {DepotId} needs no licence",
                appId, depotId);
            return DepotAccess.Granted;
        }

        // The free-content check above is the only one that means anything without a licence list, and it
        // said no. Without the list, "not entitled" is a guess -- and telling someone to go and buy a game
        // they already own because their connection was slow is a worse answer than admitting the doubt.
        return licensesArrived ? DepotAccess.NotEntitled : DepotAccess.Unconfirmed;
    }

    /// <summary>
    ///     Whether any of the account's licences names <paramref name="id" />, and whether the licence list
    ///     arrived at all. The second answer matters: "nothing we have covers it" and "we never found out
    ///     what we have" look identical here and lead somewhere completely different for the user.
    ///     <para>
    ///         The id is checked against a package's <c>appids</c> as well as its <c>depotids</c>, so this
    ///         answers for an app and for a depot alike - see <see cref="DepotEntitlement.PackageGrantsDepot" />.
    ///     </para>
    /// </summary>
    private async Task<(bool Held, bool LicensesArrived)> HoldsLicenseForAsync(uint id, CancellationToken token)
    {
        var licensesArrived = await _session.WaitForLicensesAsync(LicenseWait, token).ConfigureAwait(false);

        // The licence carries the package's access token. Without it PICS answers about the package with
        // nothing useful, so the depot list would come back empty and a perfectly entitled account would be
        // told it owns nothing.
        foreach (var license in _session.Licenses) PackageTokens[license.PackageID] = license.AccessToken;

        var packages = _session.Licenses.Select(l => l.PackageID).Distinct().ToArray();
        if (packages.Length == 0) return (false, licensesArrived);

        var infos = await GetPackageInfos(packages);

        return (infos.Values.Any(info => DepotEntitlement.PackageGrantsDepot(info?.KeyValues, id)),
            licensesArrived);
    }

    /// <inheritdoc />
    public async Task<bool> EnsureFreeLicenseAsync(uint appId, CancellationToken token)
    {
        EnsureLoggedIn();

        var (held, _) = await HoldsLicenseForAsync(appId, token).ConfigureAwait(false);
        if (held) return true;

        _logger.LogInformation(
            "The Steam account {Account} holds no licence for app {AppId}, asking Steam for the free one",
            _session.AccountName, appId);

        var granted = await _session.Apps.RequestFreeLicense(appId);

        if (granted.Result != EResult.OK || !granted.GrantedApps.Contains(appId))
        {
            _logger.LogInformation("Steam would not grant a free licence for app {AppId}: {Result}", appId,
                granted.Result);
            return false;
        }

        _logger.LogInformation("Steam granted a free licence for app {AppId} as package {Packages}", appId,
            string.Join(", ", granted.GrantedPackages));

        // Steam follows the grant with a fresh licence list, and the new packages will not be in anything
        // already worked out from the old one. Drop them so the next entitlement question asks about them.
        foreach (var package in granted.GrantedPackages) PackageInfos.TryRemove(package, out _);

        return true;
    }

    /// <summary>As <see cref="CheckAccessAsync" />, but says so rather than returning an answer.</summary>
    public async Task EnsureAccessAsync(uint appId, uint depotId, CancellationToken token)
    {
        switch (await CheckAccessAsync(appId, depotId, token).ConfigureAwait(false))
        {
            case DepotAccess.Granted:
                return;

            case DepotAccess.Unconfirmed:
                throw new SteamEntitlementUnconfirmedException(
                    $"Could not confirm what the Steam account {_session.AccountName} has a licence for: Steam " +
                    $"did not send the licence list within {LicenseWait.TotalSeconds:0}s. This says nothing " +
                    $"about whether you own depot {depotId} of app {appId}. Check your connection and try again.",
                    appId, depotId);

            default:
                throw new SteamNoEntitlementException(
                    $"The Steam account {_session.AccountName} holds no licence for depot {depotId} of app " +
                    $"{appId}, and the app is not free to download. Log in with an account that owns it.",
                    appId, depotId);
        }
    }

    public async ValueTask<byte[]> GetDepotKey(uint depotId, uint appId)
    {
        if (DepotKeys.TryGetValue(depotId, out var cached))
            return cached;

        EnsureLoggedIn();
        _logger.LogInformation("Requesting depot key for {DepotId}", depotId);

        var result = await _session.Apps.GetDepotDecryptionKey(depotId, appId);
        if (result.Result != EResult.OK)
            throw new SteamException($"Error getting depot key for {depotId} {appId}", result.Result, EResult.Invalid);

        DepotKeys[depotId] = result.DepotKey;
        return result.DepotKey;
    }

    /// <summary>
    ///     Downloads and decrypts a depot manifest: the list of every file in that depot at that version,
    ///     with the chunks each is made of. Fetches every time it is called; <see cref="GetFilesAsync" /> is
    ///     the one that remembers, and is what everything else here goes through.
    /// </summary>
    private async Task<DepotManifest> GetManifestAsync(uint appId, uint depotId, ulong manifestId,
        CancellationToken token, string branch = PublicBranch)
    {
        EnsureLoggedIn();

        var depotKey = await GetDepotKey(depotId, appId).ConfigureAwait(false);
        var cdn = GetCdn();

        var manifest = await WithServerAsync(appId, depotId, async (server, proxy, authToken) =>
        {
            var code = await GetManifestRequestCodeAsync(appId, depotId, manifestId, branch, token)
                .ConfigureAwait(false);

            try
            {
                return await cdn.DownloadManifestAsync(depotId, manifestId, code, server, depotKey, proxy,
                    authToken).ConfigureAwait(false);
            }
            catch
            {
                // A refusal here is as likely to be a stale request code as a bad server, and a code costs
                // one round trip to replace. Throw it away so the next attempt asks for a new one.
                _codes.Forget(depotId, manifestId, branch);
                throw;
            }
        }, token).ConfigureAwait(false);

        if (manifest.FilenamesEncrypted)
            manifest.DecryptFilenames(depotKey);

        _logger.LogInformation(
            "Manifest {ManifestId} of depot {DepotId} lists {Count} entries, {Bytes} bytes uncompressed",
            manifestId, depotId, manifest.Files?.Count ?? 0, manifest.TotalUncompressedSize);

        return manifest;
    }

    /// <summary>
    ///     A manifest's file list, downloading it only if it is not already held. The list is read-only and
    ///     several callers share it, so it is never sorted, filtered or added to in place.
    /// </summary>
    private async Task<IReadOnlyList<DepotManifest.FileData>> GetFilesAsync(uint appId, uint depotId,
        ulong manifestId, CancellationToken token, string branch)
    {
        if (_manifestFiles.Get(depotId, manifestId, branch) is { } cached) return cached;

        var manifest = await GetManifestAsync(appId, depotId, manifestId, token, branch).ConfigureAwait(false);

        // A copy, not the manifest's own List: the manifest itself is dropped here, and what is kept has to
        // be something no other reference reaches.
        var files = new ReadOnlyCollection<DepotManifest.FileData>(
            (manifest.Files ?? new List<DepotManifest.FileData>()).ToArray());

        // Two callers racing on the same manifest both downloaded it and both have the same immutable
        // build, so the first one stored wins and the second is discarded. Holding a lock across the
        // download to prevent the race would serialise every depot search a repair makes.
        _manifestFiles.Set(depotId, manifestId, branch, files);
        return _manifestFiles.Get(depotId, manifestId, branch) ?? files;
    }

    /// <summary>
    ///     Looks <paramref name="depotPath" /> up in a manifest without fetching any content. A caller
    ///     hunting one file through several depots asks this of each in turn and only downloads from the one
    ///     that answers.
    /// </summary>
    public async Task<DepotFile?> FindFileAsync(uint appId, uint depotId, ulong manifestId, string depotPath,
        CancellationToken token, string branch = PublicBranch)
    {
        var files = await GetFilesAsync(appId, depotId, manifestId, token, branch).ConfigureAwait(false);
        var file = DepotPaths.Find(files, depotPath);
        return file == null ? null : Describe(file);
    }

    /// <summary>
    ///     Fetches one named file out of a depot and writes it to <paramref name="output" />, verified
    ///     against the hash the manifest carries for it.
    ///     Nothing lands at <paramref name="output" /> until the hash matches: the bytes are assembled beside
    ///     it and moved into place afterwards, so a failed or cancelled fetch cannot leave something that
    ///     looks like a game file but is not one.
    /// </summary>
    /// <returns>The manifest entry that was downloaded, so a caller can report what it actually matched.</returns>
    public async Task<DepotFile> DownloadFileAsync(uint appId, uint depotId, ulong manifestId,
        string depotPath, AbsolutePath output, CancellationToken token, IJob? parentJob = null,
        string branch = PublicBranch)
    {
        await EnsureAccessAsync(appId, depotId, token).ConfigureAwait(false);

        var files = await GetFilesAsync(appId, depotId, manifestId, token, branch).ConfigureAwait(false);

        var file = DepotPaths.Find(files, depotPath)
                   ?? throw new SteamFileNotInDepotException(
                       $"Manifest {manifestId} of depot {depotId} has no file matching '{depotPath}'", depotPath);

        await DownloadFileAsync(appId, depotId, file, output, token, parentJob).ConfigureAwait(false);
        return Describe(file);
    }

    /// <summary>
    ///     Every file a manifest lists, without downloading any of them. Directory entries are left out:
    ///     they carry a path and no content, and nothing downstream has any use for them.
    /// </summary>
    public async Task<IReadOnlyList<DepotFile>> ListFilesAsync(uint appId, uint depotId, ulong manifestId,
        CancellationToken token, string branch = PublicBranch)
    {
        var files = await GetFilesAsync(appId, depotId, manifestId, token, branch).ConfigureAwait(false);

        return files
            .Where(f => !f.Flags.HasFlag(EDepotFileFlag.Directory))
            .Select(Describe)
            .ToArray();
    }

    private static DepotFile Describe(DepotManifest.FileData file)
    {
        return new DepotFile(file.FileName, file.TotalSize,
            file.FileHash is {Length: > 0} hash ? Convert.ToHexString(hash) : string.Empty);
    }

    /// <summary>
    ///     As above, for a caller that already holds the manifest entry -- which is the case once a whole
    ///     manifest has been read to decide what is missing.
    /// </summary>
    public async Task DownloadFileAsync(uint appId, uint depotId, DepotManifest.FileData file,
        AbsolutePath output, CancellationToken token, IJob? parentJob = null)
    {
        if (file.Flags.HasFlag(EDepotFileFlag.Directory))
            throw new SteamFileNotInDepotException($"'{file.FileName}' is a directory, not a file", file.FileName);

        var depotKey = await GetDepotKey(depotId, appId).ConfigureAwait(false);
        var cdn = GetCdn();

        await _assembler.AssembleAsync(file, output,
            (chunk, buffer, chunkToken) => WithServerAsync(appId, depotId,
                (server, proxy, authToken) => cdn.DownloadDepotChunkAsync(depotId, chunk, server, buffer,
                    depotKey, proxy, authToken), chunkToken),
            token, parentJob).ConfigureAwait(false);

        _logger.LogInformation("Fetched {FileName} ({Bytes} bytes) from depot {DepotId}", file.FileName,
            file.TotalSize, depotId);
    }

    private async Task<SteamApps.PICSProductInfoCallback.PICSProductInfo> GetAppProductInfo(uint appId)
    {
        if (AppProductInfos.TryGetValue(appId, out var cached))
            return cached;

        EnsureLoggedIn();

        var result = await _session.Apps.PICSGetAccessTokens(new List<uint> {appId}, new List<uint>());

        if (result.AppTokensDenied.Contains(appId))
            throw new SteamException($"Cannot get app token for {appId}", EResult.Invalid, EResult.Invalid);

        foreach (var token in result.AppTokens) AppTokens[token.Key] = token.Value;

        var request = new SteamApps.PICSRequest(appId);
        if (AppTokens.TryGetValue(appId, out var appToken)) request.AccessToken = appToken;

        var appResult = await _session.Apps.PICSGetProductInfo(new List<SteamApps.PICSRequest> {request},
            new List<SteamApps.PICSRequest>());

        if (appResult.Failed || appResult.Results == null)
            throw new SteamException($"Error getting app info for {appId}", EResult.Invalid, EResult.Invalid);

        foreach (var (_, value) in appResult.Results.SelectMany(v => v.Apps)) AppProductInfos[value.ID] = value;

        if (!AppProductInfos.TryGetValue(appId, out var fetched))
            throw new SteamException($"Steam knows nothing about app {appId}", EResult.Invalid, EResult.Invalid);

        return fetched;
    }

    /// <summary>
    ///     A manifest cannot be fetched without one of these, and they expire. A zero back is Steam saying no
    ///     -- almost always no entitlement, or a manifest that has been withdrawn -- and retrying a no gets
    ///     another no, so it is reported rather than retried.
    /// </summary>
    private async Task<ulong> GetManifestRequestCodeAsync(uint appId, uint depotId, ulong manifestId,
        string branch, CancellationToken token)
    {
        if (_codes.Get(depotId, manifestId, branch) is { } cached) return cached;

        var code = await _session.Content.GetManifestRequestCode(depotId, appId, manifestId, branch, null)
            .ConfigureAwait(false);

        if (code == 0)
            throw new SteamManifestUnavailableException(
                $"Steam would not issue a request code for manifest {manifestId} of depot {depotId} (app {appId}). " +
                "That means either the account cannot reach this depot or the manifest is no longer served.",
                appId, depotId, manifestId);

        _codes.Set(depotId, manifestId, branch, code);
        return code;
    }

    /// <summary>
    ///     Runs a CDN request against servers from the pool, dealing with the two failures that are not the
    ///     caller's problem: a server that wants a CDN auth token, and a server that is simply not serving.
    /// </summary>
    private async Task<T> WithServerAsync<T>(uint appId, uint depotId,
        Func<Server, Server?, string?, Task<T>> action, CancellationToken token)
    {
        var pool = GetPool();
        Exception? last = null;

        for (var attempt = 0; attempt < MaxServerAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var server = await pool.TakeAsync(appId, token).ConfigureAwait(false);
            var host = server.Host ?? string.Empty;

            _cdnAuthTokens.TryGetValue((depotId, host), out var authToken);

            try
            {
                return await action(server, pool.ProxyServer, authToken).ConfigureAwait(false);
            }
            catch (SteamKitWebRequestException ex) when (NeedsAuthToken(ex))
            {
                // Either this host has never been authorised for the depot, or the token it was given has
                // gone stale. Both are fixed the same way, and both are worth exactly one retry.
                var fresh = await RequestCdnAuthTokenAsync(appId, depotId, host, token).ConfigureAwait(false);
                if (fresh == null)
                {
                    pool.StrikeOff(server);
                    last = ex;
                    continue;
                }

                try
                {
                    return await action(server, pool.ProxyServer, fresh).ConfigureAwait(false);
                }
                catch (Exception retried)
                {
                    pool.StrikeOff(server);
                    last = retried;
                }
            }
            catch (Exception ex) when (ex is SteamKitWebRequestException or HttpRequestException
                                           or IOException or TaskCanceledException &&
                                       !token.IsCancellationRequested)
            {
                _logger.LogWarning("Steam content server {Host} failed: {Message}", host, ex.Message);
                pool.StrikeOff(server);
                last = ex;
            }
        }

        throw new SteamException(
            $"No Steam content server would serve depot {depotId} of app {appId} after {MaxServerAttempts} attempts",
            EResult.Fail, EResult.Invalid, last);
    }

    private static bool NeedsAuthToken(SteamKitWebRequestException ex)
    {
        return ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
    }

    private async Task<string?> RequestCdnAuthTokenAsync(uint appId, uint depotId, string host,
        CancellationToken token)
    {
        var result = await _session.Content.GetCDNAuthToken(appId, depotId, host).ConfigureAwait(false);

        if (result.Result != EResult.OK || string.IsNullOrEmpty(result.Token))
        {
            _logger.LogWarning("Steam would not issue a CDN auth token for {Host} on depot {DepotId} ({Result})",
                host, depotId, result.Result);
            return null;
        }

        _cdnAuthTokens[(depotId, host)] = result.Token;
        _logger.LogInformation("Got a CDN auth token for {Host} on depot {DepotId}, good until {Expiry}",
            host, depotId, result.Expiration);

        return result.Token;
    }

    private Client GetCdn()
    {
        lock (_poolLock)
        {
            return _cdn ??= _session.CreateCdnClient();
        }
    }

    private CdnServerPool<Server> GetPool()
    {
        if (_pool != null) return _pool;

        EnsureLoggedIn();

        lock (_poolLock)
        {
            // The cell is Steam's idea of where this machine is, and handing it back is what gets a nearby
            // server rather than an arbitrary one.
            var cellId = _session.CellId;

            return _pool ??= new CdnServerPool<Server>(_logger, cellId,
                async _ => await _session.Content.GetServersForSteamPipe(cellId).ConfigureAwait(false),
                s => new ContentServerFacts($"{s.Host}:{s.Port}", s.Type, s.AllowedAppIds, s.NumEntries,
                    s.WeightedLoad, s.UseAsProxy));
        }
    }

    private void EnsureLoggedIn()
    {
        if (!_session.IsLoggedIn)
            throw new SteamLoginRequiredException("Not logged into Steam");
    }
}
