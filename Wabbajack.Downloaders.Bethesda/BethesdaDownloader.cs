using System.IO.Compression;
using System.Security.Cryptography;
using CS_AES_CTR;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.BethesdaNet;
using Wabbajack.Networking.BethesdaNet.DTOs;
using Wabbajack.Networking.Http;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.Bethesda;

public class BethesdaDownloader : ADownloader<DTOs.DownloadStates.Bethesda>, IUrlDownloader, IChunkedSeekableStreamDownloader
{
    private readonly Client _client;
    private readonly IResource<HttpClient> _limiter;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BethesdaDownloader> _logger;

    public BethesdaDownloader(ILogger<BethesdaDownloader> logger, Client client, HttpClient httpClient, IResource<HttpClient> limiter)
    {
        _logger = logger;
        _client = client;
        _limiter = limiter;
        _httpClient = httpClient;
    }
    
    
    public override async Task<Hash> Download(Archive archive, DTOs.DownloadStates.Bethesda state, AbsolutePath destination, IJob job, CancellationToken token)
    {

        var depot = await _client.GetDepots(state, token);
        var tree = await _client.GetTree(state, token);

        var chunks = tree!.DepotList.First().FileList.First().ChunkList;

        using var os = destination.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        
        var hasher = new xxHashAlgorithm(0);
        Hash finalHash = default;

        var aesKey = depot.ExInfoA.ToArray();
        var aesIV = depot.ExInfoB.Take(16).ToArray();

        await chunks.PMapAll(async chunk =>
        {
            var data = await GetChunk(state, chunk, depot.PropertiesId, token);
            var reported = job.Report(data.Length, token);
            
            var aesCtr = new AES_CTR(aesKey, aesIV, false);
            data = aesCtr.DecryptBytes(data);

            if (chunk.UncompressedSize != chunk.ChunkSize)
            {
                var inflater = new InflaterInputStream(new MemoryStream(data));
                data = await inflater.ReadAllAsync();
            }
            
            await reported;
            return data;
        })
        .Do(async data =>
        {
            if (data.Length < tree.DepotList.First().BytesPerChunk)
            {
                hasher.HashBytes(data);
            }
            else
            {
                finalHash = Hash.FromULong(hasher.FinalizeHashValueInternal(data));
            }
            
            await os.WriteAsync(data, token);
        });
        
        return finalHash;
    }

    private async Task<byte[]> GetChunk(DTOs.DownloadStates.Bethesda state, Chunk chunk, long propertiesId,
        CancellationToken token)
    {
        var uri = new Uri($"https://content.cdp.bethesda.net/{state.ProductId}/{propertiesId}/{chunk.Sha}");
        var msg = new HttpRequestMessage(HttpMethod.Get, uri);
        msg.Headers.Add("User-Agent", "bnet");
        using var job = await _limiter.Begin("Getting chunk", chunk.ChunkSize, token);
        using var response = await _httpClient.SendAsync(msg, token);
        if (!response.IsSuccessStatusCode)
            throw new HttpException(response);
        await job.Report(chunk.ChunkSize, token);
        return await response.Content.ReadAsByteArrayAsync(token);
    }

    public override async Task<bool> Prepare()
    {
        await _client.CDPAuth(CancellationToken.None);
        return true;
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") && Uri.TryCreate(iniData["directURL"].CleanIniString(), UriKind.Absolute, out var uri))
        {
            return Parse(uri);
        }
        return null;
    }

    public override Priority Priority => Priority.Normal;

    public override async Task<bool> Verify(Archive archive, DTOs.DownloadStates.Bethesda state, IJob job, CancellationToken token)
    {
        var depot = await _client.GetDepots(state, token);
        return depot != null;
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.Bethesda state)
    {
        return new[] {$"directURL={UnParse(state)}"};
    }

    public IDownloadState? Parse(Uri uri)
    {
        if (uri.Scheme != "bethesda") return null;
        var path = uri.PathAndQuery.Split("/", StringSplitOptions.RemoveEmptyEntries);
        if (path.Length != 4) return null;
        var game = GameRegistry.TryGetByFuzzyName(uri.Host);
        if (game == null) return null;

        if (!long.TryParse(path[1], out var productId)) return null;
        if (!long.TryParse(path[2], out var branchId)) return null;
        
        bool isCCMod = false;
        switch (path[0])
        {
            case "cc":
                isCCMod = true;
                break;
            case "mod":
                isCCMod = false;
                break;
            default:
                return null;
        }

        return new DTOs.DownloadStates.Bethesda
        {
            Game = game.Game,
            IsCCMod = isCCMod,
            ProductId = productId,
            BranchId = branchId,
            ContentId = path[3]
        };
    }

    public Uri UnParse(IDownloadState state)
    {
        var cstate = (DTOs.DownloadStates.Bethesda) state;
        return new Uri($"bethesda://{cstate.Game}/{(cstate.IsCCMod ? "cc" : "mod")}/{cstate.ProductId}/{cstate.BranchId}/{cstate.ContentId}");
    }

    public ValueTask<Stream> GetChunkedSeekableStream(Archive archive, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}