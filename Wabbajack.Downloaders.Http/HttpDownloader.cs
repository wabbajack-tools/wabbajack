using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.Http;

public class HttpDownloader : ADownloader<DTOs.DownloadStates.Http>, IUrlDownloader, IUpgradingDownloader, IChunkedSeekableStreamDownloader
{
    private readonly HttpClient _client;
    private readonly IHttpDownloader _downloader;
    private readonly ILogger<HttpDownloader> _logger;

    public HttpDownloader(ILogger<HttpDownloader> logger, HttpClient client, IHttpDownloader downloader)
    {
        _client = client;
        _logger = logger;
        _downloader = downloader;
    }

    public async Task<Archive?> TryGetUpgrade(Archive archive, IJob job, TemporaryFileManager temporaryFileManager,
        CancellationToken token)
    {
        var state = (DTOs.DownloadStates.Http) archive.State;
        await using var file = temporaryFileManager.CreateFile();

        var newHash = await Download(archive, file.Path, job, token);

        return new Archive
        {
            Hash = newHash,
            Size = file.Path.Size(),
            State = archive.State,
            Name = archive.Name
        };
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") && Uri.TryCreate(iniData["directURL"], UriKind.Absolute, out var uri))
        {
            var state = new DTOs.DownloadStates.Http
            {
                Url = uri
            };

            if (iniData.TryGetValue("directURLHeaders", out var headers)) state.Headers = headers.Split("|").ToArray();

            return state;
        }

        return null;
    }

    public override async Task<bool> Prepare()
    {
        return true;
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return allowList.AllowedPrefixes.Any(p => ((DTOs.DownloadStates.Http) state).Url.ToString().StartsWith(p));
    }

    public IDownloadState? Parse(Uri uri)
    {
        return new DTOs.DownloadStates.Http {Url = uri};
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((DTOs.DownloadStates.Http) state).Url;
    }

    public override Priority Priority => Priority.Low;

    public override async Task<Hash> Download(Archive archive, DTOs.DownloadStates.Http state,
        AbsolutePath destination, IJob job, CancellationToken token)
    {
        return await _downloader.Download(MakeMessage(state), destination, job, token);
    }

    private async Task<HttpResponseMessage> GetResponse(DTOs.DownloadStates.Http state, CancellationToken token)
    {
        var msg = MakeMessage(state);

        return await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead,  token);
    }

    internal static HttpRequestMessage MakeMessage(DTOs.DownloadStates.Http state)
    {
        var msg = new HttpRequestMessage(HttpMethod.Get, state.Url);
        foreach (var header in state.Headers)
        {
            var idx = header.IndexOf(':');
            var k = header[..idx];
            var v = header[(idx + 1)..];
            msg.Headers.Add(k, v);
        }

        msg.AddChromeAgent();

        return msg;
    }

    public override async Task<bool> Verify(Archive archive, DTOs.DownloadStates.Http archiveState,
        IJob job, CancellationToken token)
    {
        var response = await GetResponse(archiveState, token);
        if (!response.IsSuccessStatusCode) return false;

        var headerVar = archive.Size == 0 ? "1" : archive.Size.ToString();
        ulong headerContentSize = 0;
        if (response.Content.Headers.Contains("Content-Length"))
        {
            headerVar = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();
            if (headerVar != null)
                if (!ulong.TryParse(headerVar, out headerContentSize))
                    return true;
        }

        job.Size = 1024;
        await job.Report(1024, token);

        response.Dispose();
        if (archive.Size != 0 && headerContentSize != 0)
            return archive.Size == (long) headerContentSize;
        return true;
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.Http state)
    {
        if (state.Headers.Length > 0)
            return new[]
            {
                $"directURL={state.Url}",
                $"directURLHeaders={string.Join("|", state.Headers)}"
            };
        return new[] {$"directURL={state.Url}"};
    }

    public async ValueTask<Stream> GetChunkedSeekableStream(Archive archive, CancellationToken token)
    {
        var state = archive.State as DTOs.DownloadStates.Http;
        return new ChunkedSeekableDownloader(this, archive, state!);
    }
    
    public class ChunkedSeekableDownloader : AChunkedBufferingStream
    {
        private readonly DTOs.DownloadStates.Http _state;
        private readonly Archive _archive;
        private readonly HttpDownloader _downloader;

        public ChunkedSeekableDownloader(HttpDownloader downloader, Archive archive, DTOs.DownloadStates.Http state) : base(21, archive.Size, 8)
        {
            _downloader = downloader;
            _archive = archive;
            _state = state;
        }

        public override async Task<byte[]> LoadChunk(long offset, int size)
        {
            return await CircuitBreaker.WithAutoRetryAllAsync(_downloader._logger, async () =>
            {
                var msg = HttpDownloader.MakeMessage(_state);
                msg.Headers.Range = new RangeHeaderValue(offset, offset + size);
                using var response = await _downloader._client.SendAsync(msg);
                if (!response.IsSuccessStatusCode)
                    throw new HttpException(response);

                return await response.Content.ReadAsByteArrayAsync();
            });
        }
    }
}