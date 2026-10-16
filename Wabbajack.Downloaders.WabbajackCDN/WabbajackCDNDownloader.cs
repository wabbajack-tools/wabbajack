using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders;

public class WabbajackCDNDownloader : ADownloader<WabbajackCDN>, IUrlDownloader, IChunkedSeekableStreamDownloader
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
    private readonly IResource<HttpClient> _limiter;

    public WabbajackCDNDownloader(ILogger<WabbajackCDNDownloader> logger, HttpClient client, IResource<HttpClient> limiter,  DTOSerializer dtos)
    {
        _client = client;
        _logger = logger;
        _dtos = dtos;
        _limiter = limiter;
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
        if (iniData.ContainsKey("directURL") && Uri.TryCreate(iniData["directURL"].CleanIniString(), UriKind.Absolute, out var uri))
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
        var definition = await WithRetries($"the definition of {state.Url}", async () =>
        {
            var msg = MakeMessage(new Uri(state.Url + "/definition.json.gz"));
            using var response = await _client.SendAsync(msg, token);
            // Gone is gone; anything else (a busy CDN, a 5xx, a dropped connection) is worth asking again.
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new PermanentCdnException(new HttpException(response));
            if (!response.IsSuccessStatusCode)
                throw new HttpException(response);
            return await ReadDefinition(response, token);
        }, token);
        await using var fs = destination.Open(FileMode.Create, FileAccess.Write, FileShare.None);

        await definition.Parts.PMapAll<PartDefinition, (MemoryStream, PartDefinition)>(async part =>
        {
            return await WithRetries($"part {part.Index} of {definition.MungedName}", async () =>
            {
                using var partJob = await _limiter.Begin(
                    $"Downloading {definition.MungedName} ({part.Index}/{definition.Size})",
                    part.Size, token);
                var msg = MakeMessage(new Uri(state.Url + $"/parts/{part.Index}"));
                using var response = await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidDataException($"Bad response for part request for part {part.Index}");

                await using var data = await response.Content.ReadAsStreamAsync(token);

                var ms = new MemoryStream();
                var hash = await data.HashingCopy(ms, token, partJob);
                ms.Position = 0;
                if (hash != part.Hash)
                {
                    throw new Exception(
                        $"Invalid part hash {part.Index} got {hash} instead of {part.Hash} for {definition.MungedName}");
                }

                return (ms, part);
            }, token);


        }).Do(async rec =>
        {
            var (ms, part) = rec;
            fs.Position = part.Offset;
            await job.Report((int)part.Size, token);
            await ms.CopyToAsync(fs, token);
            await fs.FlushAsync(token);
        });

        return definition.Hash;
    }

    private async Task<FileDefinition?> GetDefinition(WabbajackCDN state, CancellationToken token)
    {
        _logger.LogInformation("Getting file definition for CDN download {primaryKeyString}", state.PrimaryKeyString);
        var msg = MakeMessage(new Uri(state.Url + "/definition.json.gz"));
        using var data = await _client.SendAsync(msg, token);
        if (!data.IsSuccessStatusCode) return null;

        return await ReadDefinition(data, token);
    }

    private async Task<FileDefinition> ReadDefinition(HttpResponseMessage response, CancellationToken token)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        await using var gz = new GZipStream(stream, CompressionMode.Decompress);
        return (await _dtos.DeserializeAsync<FileDefinition>(gz, token))!;
    }

    /// <summary>The pause before the first retry; each one after waits twice as long as the last.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How many times a part or the definition is asked for before the download is given up.</summary>
    public int Attempts { get; init; } = 6;

    /// <summary>
    ///     A CDN file is a definition and hundreds of parts, so a large one makes hundreds of requests and a brief
    ///     outage lands on some of them. Each request is retried with a growing pause (about half a minute in all by
    ///     default) rather than the tenth of a second it once had, which failed whole downloads over a hiccup. A
    ///     file the CDN does not have is not retried, and nor is the user cancelling.
    /// </summary>
    private async Task<T> WithRetries<T>(string what, Func<Task<T>> request, CancellationToken token)
    {
        var delay = RetryDelay;
        for (var attempt = 1;; attempt++)
        {
            try
            {
                return await request();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (PermanentCdnException ex)
            {
                throw ex.InnerException!;
            }
            catch (Exception ex) when (attempt < Attempts)
            {
                _logger.LogWarning("Fetching {What} failed ({Message}), attempt {Attempt} of {Attempts}, waiting {Delay}",
                    what, ex.Message, attempt, Attempts, delay);
                await Task.Delay(delay, token);
                delay *= 2;
            }
        }
    }

    /// <summary>Carries an error retrying cannot fix out of <see cref="WithRetries{T}" />.</summary>
    private sealed class PermanentCdnException(Exception inner) : Exception(inner.Message, inner);

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

    public async ValueTask<Stream> GetChunkedSeekableStream(Archive archive, CancellationToken token)
    {
        var state = archive.State as WabbajackCDN;
        var definition = await GetDefinition(state!, token);
        if (definition == null)
            throw new Exception("Could not get CDN definition");
        
        return new ChunkedSeekableDownloader(state!, definition!, this);
    }

    public async Task<byte[]> GetPart(WabbajackCDN state, PartDefinition part, CancellationToken token)
    {
        var msg = MakeMessage(new Uri(state.Url + $"/parts/{part.Index}"));
        using var response = await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidDataException($"Bad response for part request for part {part.Index}");

        return await response.Content.ReadAsByteArrayAsync(token);
    }

}