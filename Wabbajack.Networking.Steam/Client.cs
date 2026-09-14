using System.Collections.Concurrent;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using SteamKit2;
using SteamKit2.CDN;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Steam.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Reads content out of Steam: product info, depot keys, manifests and files. Everything about being
///     logged in lives in <see cref="SteamSession" />.
/// </summary>
public class Client
{
    private static readonly Random _random = new();
    private readonly DTOSerializer _dtos;
    private readonly HttpClient _httpClient;
    private readonly IResource<HttpClient> _limiter;
    private readonly ILogger<Client> _logger;
    private readonly SteamSession _session;

    private Server[] _cdnServers = Array.Empty<Server>();

    public Client(ILogger<Client> logger, HttpClient client, SteamSession session, DTOSerializer dtos,
        IResource<HttpClient> limiter)
    {
        _logger = logger;
        _httpClient = client;
        _session = session;
        _dtos = dtos;
        _limiter = limiter;
    }

    public IReadOnlyList<SteamApps.LicenseListCallback.License> Licenses => _session.Licenses;

    public ConcurrentDictionary<uint, ulong> PackageTokens { get; } = new();

    public ConcurrentDictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo?> PackageInfos { get; } = new();

    public ConcurrentDictionary<uint, (SteamApps.PICSProductInfoCallback.PICSProductInfo ProductInfo, AppInfo AppInfo)>
        AppInfo { get; } = new();

    public ConcurrentDictionary<uint, ulong> AppTokens { get; } = new();

    public ConcurrentDictionary<uint, byte[]> DepotKeys { get; } = new();

    public async Task<Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo?>> GetPackageInfos(
        IEnumerable<uint> packageIds)
    {
        var packages = packageIds.Where(id => !PackageInfos.ContainsKey(id)).ToList();

        if (packages.Count > 0)
        {
            var packageRequests = new List<SteamApps.PICSRequest>();

            foreach (var package in packages)
            {
                var request = new SteamApps.PICSRequest(package);
                if (PackageTokens.TryGetValue(package, out var token)) request.AccessToken = token;

                packageRequests.Add(request);
            }

            _logger.LogInformation("Requesting {Count} package infos", packageRequests.Count);

            var results = await _session.Apps.PICSGetProductInfo(new List<SteamApps.PICSRequest>(), packageRequests);

            if (results.Failed)
                throw new SteamException("Exception getting product info", EResult.Invalid, EResult.Invalid);

            foreach (var packageInfo in results.Results!)
            {
                foreach (var package in packageInfo.Packages.Select(v => v.Value)) PackageInfos[package.ID] = package;

                foreach (var package in packageInfo.UnknownPackages) PackageInfos[package] = null;
            }
        }

        return packages.Distinct().ToDictionary(p => p, p => PackageInfos[p]);
    }

    public async Task<AppInfo> GetAppInfo(uint appId)
    {
        if (AppInfo.TryGetValue(appId, out var info))
            return info.AppInfo;

        var result = await _session.Apps.PICSGetAccessTokens(new List<uint> {appId}, new List<uint>());

        if (result.AppTokensDenied.Contains(appId))
            throw new SteamException($"Cannot get app token for {appId}", EResult.Invalid, EResult.Invalid);

        foreach (var token in result.AppTokens) AppTokens[token.Key] = token.Value;

        var request = new SteamApps.PICSRequest(appId);
        if (AppTokens.TryGetValue(appId, out var appToken)) request.AccessToken = appToken;

        var appResult = await _session.Apps.PICSGetProductInfo(new List<SteamApps.PICSRequest> {request},
            new List<SteamApps.PICSRequest>());

        if (appResult.Failed)
            throw new SteamException($"Error getting app info for {appId}", EResult.Invalid, EResult.Invalid);

        foreach (var (_, value) in appResult.Results!.SelectMany(v => v.Apps))
        {
            var translated = KeyValueTranslator.Translate<AppInfo>(value.KeyValues, _dtos);
            AppInfo[value.ID] = (value, translated);
        }

        return AppInfo[appId].AppInfo;
    }

    public async Task<Server[]> LoadCDNServers()
    {
        if (_cdnServers.Length > 0) return _cdnServers;
        _logger.LogInformation("Loading CDN servers");
        _cdnServers = (await ContentServerDirectoryService.LoadAsync(_session.Configuration)).ToArray();
        _logger.LogInformation("{Count} servers found", _cdnServers.Length);

        return _cdnServers;
    }

    public async Task<DepotManifest> GetAppManifest(uint appId, uint depotId, ulong manifestId)
    {
        await LoadCDNServers();

        var manifest = await CircuitBreaker.WithAutoRetryAsync<DepotManifest, HttpRequestException>(_logger, async () =>
        {
            var client = _cdnServers.First();
            var uri = new UriBuilder
            {
                Host = client.Host,
                Port = client.Port,
                Scheme = client.Protocol.ToString(),
                Path = $"depot/{depotId}/manifest/{manifestId}/5"
            }.Uri;

            var rawData = await _httpClient.GetByteArrayAsync(uri);

            using var zip = new ZipArchive(new MemoryStream(rawData));
            var firstEntry = zip.Entries.First();
            var data = new MemoryStream();
            await using var entryStream = firstEntry.Open();
            await entryStream.CopyToAsync(data);
            return DepotManifest.Deserialize(data.ToArray());
        });

        if (manifest.FilenamesEncrypted)
            manifest.DecryptFilenames(await GetDepotKey(depotId, appId));

        return manifest;
    }

    public async ValueTask<byte[]> GetDepotKey(uint depotId, uint appId)
    {
        if (DepotKeys.TryGetValue(depotId, out var cached))
            return cached;

        _logger.LogInformation("Requesting Depot Key for {DepotId}", depotId);

        var result = await _session.Apps.GetDepotDecryptionKey(depotId, appId);
        if (result.Result != EResult.OK)
            throw new SteamException($"Error getting Depot Key for {depotId} {appId}", result.Result, EResult.Invalid);

        DepotKeys[depotId] = result.DepotKey;
        return result.DepotKey;
    }

    private Server RandomServer()
    {
        return _cdnServers[_random.Next(0, _cdnServers.Length)];
    }

    public async Task Download(uint appId, uint depotId, ulong manifest, DepotManifest.FileData fileData,
        AbsolutePath output, CancellationToken token, IJob? parentJob = null)
    {
        await LoadCDNServers();

        var depotKey = await GetDepotKey(depotId, appId);

        await using var os = output.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

        await fileData.Chunks.OrderBy(c => c.Offset)
            .PMapAll(async chunk =>
            {
                async Task<ReadOnlyMemory<byte>> AttemptDownload(DepotManifest.ChunkData chunkData)
                {
                    var client = RandomServer();
                    using var job = await _limiter.Begin($"Downloading chunk of {fileData.FileName}",
                        chunkData.CompressedLength, token);

                    var chunkId = chunkData.ChunkID!.ToHex();

                    var uri = new UriBuilder
                    {
                        Host = client.Host,
                        Port = client.Port,
                        Scheme = client.Protocol.ToString(),
                        Path = $"depot/{depotId}/chunk/{chunkId}"
                    }.Uri;

                    var data = await _httpClient.GetByteArrayAsync(uri, token);
                    await job.Report(data.Length, token);

                    if (parentJob != null)
                        await parentJob.Report(data.Length, token);

                    var destination = new byte[chunkData.UncompressedLength];
                    var written = DepotChunk.Process(chunkData, data, destination, depotKey);
                    return destination.AsMemory(0, written);
                }

                return await CircuitBreaker.WithAutoRetryAsync<ReadOnlyMemory<byte>, HttpRequestException>(_logger,
                    () => AttemptDownload(chunk));
            }).Do(async data => { await os.WriteAsync(data, token); });
    }
}
