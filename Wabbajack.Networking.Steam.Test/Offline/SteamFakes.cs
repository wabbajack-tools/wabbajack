using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Steam.DTOs;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     The stand-ins the restorer tests share: a version and content index, a content client whose depots are
///     lists of file names, and a session. Nothing here touches Steam or the network.
/// </summary>
internal sealed class FakeIndex : ISteamManifestIndex
{
    public Dictionary<string, SteamManifest[]> Versions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(Game Game, string Version)> Asked { get; } = new();

    /// <summary>What the content index knows, by the hash somebody looked up.</summary>
    public Dictionary<Hash, IndexedGameFile[]> Content { get; } = new();

    public List<Hash> Looked { get; } = new();

    /// <summary>Set to make the content index unreadable, which a repair has to survive.</summary>
    public Func<Exception>? FindThrows { get; set; }

    /// <summary>Set to make the version index unreadable.</summary>
    public Func<Exception>? GetThrows { get; set; }

    public Task<SteamManifest[]> Get(Game game, string version, CancellationToken token)
    {
        Asked.Add((game, version));
        if (GetThrows != null) throw GetThrows();

        return Task.FromResult(Versions.TryGetValue(version, out var manifests)
            ? manifests
            : Array.Empty<SteamManifest>());
    }

    public Task<IndexedGameFile[]> Find(Game game, Hash hash, CancellationToken token)
    {
        Looked.Add(hash);
        if (FindThrows != null) throw FindThrows();

        return Task.FromResult(Content.TryGetValue(hash, out var found)
            ? found
            : Array.Empty<IndexedGameFile>());
    }
}

/// <summary>
///     A depot that is a list of file names. <see cref="DepotPaths.Find" /> does the matching for real,
///     since that is the part a wrong answer would put the wrong bytes on somebody's disk.
/// </summary>
internal sealed class FakeContentClient : ISteamContentClient
{
    /// <summary>Skyrim Special Edition's own app, which is what <see cref="Current" /> publishes under.</summary>
    public const uint SkyrimSE = 489830;

    /// <summary>What each app publishes on its public branch.</summary>
    public Dictionary<uint, List<DepotManifestId>> Published { get; } = new();

    /// <summary>What the game itself publishes, which is what most of these tests are about.</summary>
    public List<DepotManifestId> Current => Publishes(SkyrimSE);

    public Dictionary<(uint Depot, ulong Manifest), string[]> Manifests { get; } = new();

    /// <summary>
    ///     What a manifest says a file weighs, by depot-relative path or by "depot:path" where one
    ///     depot's copy differs from another's. Anything not named here is one byte long.
    /// </summary>
    public Dictionary<string, ulong> Sizes { get; } = new();

    /// <summary>Depots the account has no licence for.</summary>
    public HashSet<uint> Refuses { get; } = new();

    /// <summary>
    ///     Depots whose licence list never arrived, so Steam can say neither yes nor no. Read as an app id
    ///     as well when <see cref="CheckAccessAsync" /> is asked about an app's own id.
    /// </summary>
    public HashSet<uint> Unconfirmed { get; } = new();

    /// <summary>Thrown out of <see cref="CheckAccessAsync" /> when set, as a broken connection would.</summary>
    public Func<Exception>? CheckAccessThrows { get; set; }

    /// <summary>Every (app, depot) pair <see cref="CheckAccessAsync" /> was asked about.</summary>
    public List<(uint App, uint Depot)> AccessChecked { get; } = new();

    /// <summary>What reading a depot's manifest throws, by depot, for the exceptions a search has to sort.</summary>
    public Dictionary<uint, Func<Exception>> FindThrows { get; } = new();

    /// <summary>Every manifest search that was started, including the ones that then threw.</summary>
    public List<(uint App, uint Depot, ulong Manifest)> FindAttempts { get; } = new();

    /// <summary>
    ///     Apps Steam will not so much as describe without a licence, which is what an account that has
    ///     never installed the Creation Kit meets: the PICS access token is refused, so there is no
    ///     depot list to be had.
    /// </summary>
    public HashSet<uint> NeedsLicense { get; } = new();

    /// <summary>Apps the account already holds a licence for.</summary>
    public HashSet<uint> Licensed { get; } = new();

    /// <summary>Apps Steam will hand out a free licence for when asked.</summary>
    public HashSet<uint> GrantsFreeLicense { get; } = new();

    /// <summary>Apps the restorer asked this client about.</summary>
    public List<uint> FreeLicensesAsked { get; } = new();

    /// <summary>
    ///     Apps Steam was actually asked to grant - which is the one that matters, because it is the
    ///     one that puts a package in somebody's library. An app already licensed never reaches it.
    /// </summary>
    public List<uint> GrantsRequested { get; } = new();

    public List<DepotManifestId> Publishes(uint app)
    {
        if (!Published.TryGetValue(app, out var depots)) Published[app] = depots = new List<DepotManifestId>();
        return depots;
    }

    public List<(uint App, uint Depot, ulong Manifest)> Searched { get; } = new();
    public List<(uint App, uint Depot, ulong Manifest, string Path)> Downloaded { get; } = new();

    public Task<AppInfo> GetAppInfo(uint appId)
    {
        return Task.FromResult(new AppInfo());
    }

    public Task<ulong?> GetCurrentManifestIdAsync(uint appId, uint depotId,
        string branch = SteamContentClient.PublicBranch)
    {
        return Task.FromResult(Current.FirstOrDefault(c => c.DepotId == depotId)?.ManifestId);
    }

    public Task<IReadOnlyList<DepotManifestId>> GetCurrentDepotsAsync(uint appId,
        string branch = SteamContentClient.PublicBranch)
    {
        if (NeedsLicense.Contains(appId) && !Licensed.Contains(appId))
            throw new SteamException($"Cannot get app token for {appId}", EResult.Invalid, EResult.Invalid);

        return Task.FromResult<IReadOnlyList<DepotManifestId>>(Publishes(appId).ToArray());
    }

    public Task<DepotAccess> CheckAccessAsync(uint appId, uint depotId, CancellationToken token)
    {
        AccessChecked.Add((appId, depotId));
        if (CheckAccessThrows != null) throw CheckAccessThrows();

        if (Refuses.Contains(depotId)) return Task.FromResult(DepotAccess.NotEntitled);
        if (Unconfirmed.Contains(depotId)) return Task.FromResult(DepotAccess.Unconfirmed);
        return Task.FromResult(DepotAccess.Granted);
    }

    /// <summary>
    ///     The contract <see cref="ISteamContentClient.EnsureFreeLicenseAsync" /> documents: a licence
    ///     already held is never asked for again, and the answer is remembered either way.
    /// </summary>
    public Task<bool> EnsureFreeLicenseAsync(uint appId, CancellationToken token)
    {
        FreeLicensesAsked.Add(appId);

        if (Licensed.Contains(appId)) return Task.FromResult(true);

        GrantsRequested.Add(appId);
        if (!GrantsFreeLicense.Contains(appId)) return Task.FromResult(false);

        Licensed.Add(appId);
        return Task.FromResult(true);
    }

    public Task EnsureAccessAsync(uint appId, uint depotId, CancellationToken token)
    {
        if (Refuses.Contains(depotId))
            throw new SteamNoEntitlementException(
                $"The Steam account holds no licence for depot {depotId} of app {appId}", appId, depotId);

        // The wording SteamContentClient uses, so a test can tell which of the two refusals reached the user.
        if (Unconfirmed.Contains(depotId))
            throw new SteamEntitlementUnconfirmedException(
                "Could not confirm what the Steam account tester has a licence for: Steam did not send the " +
                $"licence list within 10s. This says nothing about whether you own depot {depotId} of app " +
                $"{appId}. Check your connection and try again.", appId, depotId);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DepotFile>> ListFilesAsync(uint appId, uint depotId, ulong manifestId,
        CancellationToken token, string branch = SteamContentClient.PublicBranch)
    {
        return Task.FromResult<IReadOnlyList<DepotFile>>(Files(depotId, manifestId));
    }

    public async Task<DepotFile?> FindFileAsync(uint appId, uint depotId, ulong manifestId, string depotPath,
        CancellationToken token, string branch = SteamContentClient.PublicBranch)
    {
        FindAttempts.Add((appId, depotId, manifestId));
        if (FindThrows.TryGetValue(depotId, out var thrown)) throw thrown();

        await EnsureAccessAsync(appId, depotId, token);
        Searched.Add((appId, depotId, manifestId));
        return Match(depotId, manifestId, depotPath);
    }

    public async Task<DepotFile> DownloadFileAsync(uint appId, uint depotId, ulong manifestId, string depotPath,
        AbsolutePath output, CancellationToken token, IJob? parentJob = null,
        string branch = SteamContentClient.PublicBranch)
    {
        await EnsureAccessAsync(appId, depotId, token);
        var found = Match(depotId, manifestId, depotPath)
                    ?? throw new SteamFileNotInDepotException($"No '{depotPath}' in {depotId}", depotPath);

        Downloaded.Add((appId, depotId, manifestId, found.Path));
        return found;
    }

    /// <summary>The real matching rule, since a wrong answer here is wrong bytes on somebody's disk.</summary>
    private DepotFile? Match(uint depotId, ulong manifestId, string depotPath)
    {
        var names = Manifests.TryGetValue((depotId, manifestId), out var listed)
            ? listed
            : Array.Empty<string>();

        var match = names.FirstOrDefault(n => DepotPaths.AreSame(n, depotPath)) ??
                    names.SingleOrDefault(n => DepotPaths.EndsWithPath(n, depotPath));

        return match == null ? null : new DepotFile(match, SizeOf(depotId, match), string.Empty);
    }

    private DepotFile[] Files(uint depotId, ulong manifestId)
    {
        return Manifests.TryGetValue((depotId, manifestId), out var names)
            ? names.Select(n => new DepotFile(n, SizeOf(depotId, n), string.Empty)).ToArray()
            : Array.Empty<DepotFile>();
    }

    /// <summary>
    ///     What this depot says the file weighs: the depot's own entry when one was set, otherwise the
    ///     file's, otherwise one byte - which is what every test that is not about sizes wants.
    /// </summary>
    private ulong SizeOf(uint depotId, string path)
    {
        if (Sizes.TryGetValue($"{depotId}:{path}", out var perDepot)) return perDepot;
        return Sizes.TryGetValue(path, out var size) ? size : 1;
    }
}

internal sealed class FakeSession : ISteamSession
{
    public bool LoggedInWithStoredToken { get; private set; }
    public bool IsLoggedIn { get; set; }
    public string? AccountName { get; set; } = "tester";
    public bool HaveStoredToken { get; set; }

    /// <summary>Set to make redeeming the stored login fail, as an expired or revoked token does.</summary>
    public Func<Exception>? StoredLoginThrows { get; set; }

    /// <summary>How many times the stored login was redeemed or tried.</summary>
    public int StoredLoginAttempts { get; private set; }

    public Task<SteamLoginResult> LoginWithStoredTokenAsync(CancellationToken token)
    {
        StoredLoginAttempts++;
        if (StoredLoginThrows != null) throw StoredLoginThrows();
        if (!HaveStoredToken) throw new SteamLoginRequiredException("There is no saved Steam login.");
        LoggedInWithStoredToken = true;
        IsLoggedIn = true;
        return Task.FromResult(new SteamLoginResult("tester", 1, true, null));
    }

    public Task<SteamLoginResult> LoginWithQrCodeAsync(Action<string> onChallengeUrl, CancellationToken token)
    {
        throw new NotSupportedException();
    }

    public Task<SteamLoginResult> LoginWithCredentialsAsync(string username, string password,
        CancellationToken token)
    {
        throw new NotSupportedException();
    }

    public ValueTask<SteamLogoutResult> LogoutAsync()
    {
        return ValueTask.FromResult(SteamLogoutResult.NothingStored);
    }

    public void Dispose()
    {
    }
}
