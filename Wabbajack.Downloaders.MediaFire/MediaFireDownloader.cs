using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.MediaFire;

public class MediaFireDownloader : ADownloader<DTOs.DownloadStates.MediaFire>, IUrlDownloader, IProxyable
{
    private readonly IHttpDownloader _downloader;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MediaFireDownloader> _logger;

    public MediaFireDownloader(ILogger<MediaFireDownloader> logger, HttpClient httpClient, IHttpDownloader downloader)
    {
        _logger = logger;
        _httpClient = httpClient;
        _downloader = downloader;
    }


    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        var mediaFireState = (DTOs.DownloadStates.MediaFire) state;
        return allowList.AllowedPrefixes.Any(p => mediaFireState.Url.ToString().StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") &&
            Uri.TryCreate(iniData["directURL"].CleanIniString(), UriKind.Absolute, out var uri)
            && uri.Host == "www.mediafire.com")
        {
            var state = new DTOs.DownloadStates.MediaFire
            {
                Url = uri
            };
            return state;
        }

        return null;
    }

    public override Priority Priority => Priority.Normal;

    public IDownloadState? Parse(Uri uri)
    {
        if (uri.Host != "www.mediafire.com")
            return null;
        return new DTOs.DownloadStates.MediaFire {Url = uri};
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((DTOs.DownloadStates.MediaFire) state).Url;
    }

    public async Task<T> DownloadStream<T>(Archive archive, Func<Stream, Task<T>> fn, CancellationToken token)
    {
        var state = archive.State as DTOs.DownloadStates.MediaFire;
        var url = await Resolve(state!);
        var msg = new HttpRequestMessage(HttpMethod.Get, url!);
        using var result = await _httpClient.SendAsync(msg, token);
        await using var stream = await result.Content.ReadAsStreamAsync(token);
        return await fn(stream);
    }

    public override async Task<Hash> Download(Archive archive, DTOs.DownloadStates.MediaFire state,
        AbsolutePath destination, IJob job, CancellationToken token)
    {
        var url = await Resolve(state, job);
        var msg = new HttpRequestMessage(HttpMethod.Get, url!);
        return await _downloader.Download(msg, destination, job, token);
    }

    public override async Task<bool> Verify(Archive archive, DTOs.DownloadStates.MediaFire archiveState, IJob job,
        CancellationToken token)
    {
        return await Resolve(archiveState, job, token) != null;
    }

    private async Task<Uri?> Resolve(DTOs.DownloadStates.MediaFire state, IJob? job = null, CancellationToken? token = null)
    {
        token ??= CancellationToken.None;
        using var result = await _httpClient.GetAsync(state.Url, HttpCompletionOption.ResponseHeadersRead,
            (CancellationToken) token);
        if (!result.IsSuccessStatusCode)
            return null;

        if (job != null)
            job.Size = result.Content.Headers.ContentLength ?? 0;

        if (result.Content.Headers.ContentType!.MediaType!.StartsWith("text/html",
            StringComparison.OrdinalIgnoreCase))
        {
            var bodyData = await result.Content.ReadAsStringAsync((CancellationToken) token);
            if (job != null)
                await job.Report((int) (job.Size ?? 0), (CancellationToken) token);
            var body = new HtmlDocument();
            body.LoadHtml(bodyData);
            var node = body.DocumentNode.DescendantsAndSelf().FirstOrDefault(d => d.HasClass("input") && d.HasClass("popsok") &&
                                                                         d.GetAttributeValue("aria-label", "") ==
                                                                         "Download file");
            if (node != null)
                return new Uri(node.GetAttributeValue("href", "not-found"));

            var startText = "window.location.href = '";
            var start = body.DocumentNode.InnerHtml.IndexOf(startText, StringComparison.CurrentCultureIgnoreCase);

            if (start != -1)
            {
                var end = body.DocumentNode.InnerHtml.IndexOf("\'", start + startText.Length,
                    StringComparison.CurrentCultureIgnoreCase);
                var data = body.DocumentNode.InnerHtml[(start + startText.Length)..end];
                return new Uri(data);
            }
        }

        return state.Url;
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.MediaFire state)
    {
        return new[] {$"directURL={state.Url}"};
    }
}
