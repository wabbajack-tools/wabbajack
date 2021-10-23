using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders;

public class WabbajackCDNDownloader : ADownloader<WabbajackCDN>, IUrlDownloader
{
    public static Dictionary<string, string> DomainRemaps = new()
    {
        {"wabbajack.b-cdn.net", "authored-files.wabbajack.org"},
        {"wabbajack-mirror.b-cdn.net", "mirror.wabbajack.org"},
        {"wabbajack-patches.b-cdn.net", "patches.wabbajack.org"},
        {"wabbajacktest.b-cdn.net", "test-files.wabbajack.org"}
    };

    private readonly HttpClient _client;
    private readonly DTOSerializer _dtos;
    private readonly ILogger<WabbajackCDNDownloader> _logger;
    private readonly ParallelOptions _parallelOptions;

    public WabbajackCDNDownloader(ILogger<WabbajackCDNDownloader> logger, HttpClient client, DTOSerializer dtos)
    {
        _client = client;
        _logger = logger;
        _dtos = dtos;
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") && Uri.TryCreate(iniData["directURL"], UriKind.Absolute, out var uri))
            return Parse(uri);
        return null;
    }

    public IDownloadState? Parse(Uri url)
    {
        if (DomainRemaps.ContainsKey(url.Host) || DomainRemaps.ContainsValue(url.Host))
            return new WabbajackCDN {Url = url};
        return null;
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((WabbajackCDN) state).Url;
    }

    public override Priority Priority => Priority.Normal;

    public override async Task<Hash> Download(Archive archive, WabbajackCDN state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        var definition = (await GetDefinition(state, token))!;
        await using var fs = destination.Open(FileMode.Create, FileAccess.Write, FileShare.None);

        foreach (var part in definition.Parts)
        {
            var msg = MakeMessage(new Uri(state.Url + $"/parts/{part.Index}"));
            using var response = await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidDataException($"Bad response for part request for part {part.Index}");

            var length = response.Content.Headers.ContentLength;
            if (length != part.Size)
                throw new InvalidDataException(
                    $"Bad part size, expected {part.Size} got {length} for part {part.Index}");

            await using var data = await response.Content.ReadAsStreamAsync(token);

            fs.Position = part.Offset;
            var hash = await data.HashingCopy(fs, token, job);
            if (hash != part.Hash)
                throw new InvalidDataException($"Bad part hash, got {hash} expected {part.Hash} for part {part.Index}");
            await fs.FlushAsync(token);
        }

        return definition.Hash;
    }

    private async Task<FileDefinition?> GetDefinition(WabbajackCDN state, CancellationToken token)
    {
        var msg = MakeMessage(new Uri(state.Url + "/definition.json.gz"));
        using var data = await _client.SendAsync(msg, token);
        if (!data.IsSuccessStatusCode) return null;

        await using var stream = await data.Content.ReadAsStreamAsync(token);
        await using var gz = new GZipStream(stream, CompressionMode.Decompress);
        return (await _dtos.DeserializeAsync<FileDefinition>(gz, token))!;
    }

    private HttpRequestMessage MakeMessage(Uri url)
    {
        if (DomainRemaps.TryGetValue(url.Host, out var host))
            url = new UriBuilder(url) {Host = host}.Uri;
        else
            host = url.Host;

        var msg = new HttpRequestMessage(HttpMethod.Get, url);
        msg.Headers.Add("Host", host);
        return msg;
    }

    public override async Task<bool> Verify(Archive archive, WabbajackCDN archiveState, IJob job,
        CancellationToken token)
    {
        return await GetDefinition(archiveState, token) != null;
    }

    public override IEnumerable<string> MetaIni(Archive a, WabbajackCDN state)
    {
        return new[] {$"directURL={state.Url}"};
    }
}