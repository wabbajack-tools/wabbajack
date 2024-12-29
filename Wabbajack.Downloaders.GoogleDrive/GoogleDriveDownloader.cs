using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
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
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.GoogleDrive;

public class GoogleDriveDownloader : ADownloader<DTOs.DownloadStates.GoogleDrive>, IUrlDownloader, IProxyable
{
    private static readonly Regex GDriveRegex = new("((?<=id=)[a-zA-Z0-9_-]*)|(?<=\\/file\\/d\\/)[a-zA-Z0-9_-]*",
        RegexOptions.Compiled);

    private readonly HttpClient _client;
    private readonly IHttpDownloader _downloader;
    private readonly ILogger<GoogleDriveDownloader> _logger;

    public GoogleDriveDownloader(ILogger<GoogleDriveDownloader> logger, HttpClient client,
        IHttpDownloader downloader)
    {
        _logger = logger;
        _client = client;
        _downloader = downloader;
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return allowList.GoogleIDs.Contains(((DTOs.DownloadStates.GoogleDrive)state).Id);
    }

    public IDownloadState? Parse(Uri uri)
    {
        if (uri.Host != "drive.google.com") return null;
        var match = GDriveRegex.Match(uri.ToString());
        if (match.Success)
            return new DTOs.DownloadStates.GoogleDrive { Id = match.ToString() };
        _logger.LogWarning($"Tried to parse drive.google.com Url but couldn't get an id from: {uri}");
        return null;
    }

    public Uri UnParse(IDownloadState state)
    {
        return new Uri(
            $"https://drive.google.com/uc?id={(state as DTOs.DownloadStates.GoogleDrive)?.Id}&export=download");
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") && Uri.TryCreate(iniData["directURL"].CleanIniString(), UriKind.Absolute, out var uri))
            return Parse(uri);
        return null;
    }

    public override Priority Priority => Priority.Normal;


    public async Task<T> DownloadStream<T>(Archive archive, Func<Stream, Task<T>> fn, CancellationToken token)
    {
        var state = archive.State as DTOs.DownloadStates.GoogleDrive;
        var msg = await ToMessage(state, true, token);
        using var result = await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
        HttpException.ThrowOnFailure(result);
        await using var stream = await result.Content.ReadAsStreamAsync(token);
        return await fn(stream);
    }

    public override async Task<Hash> Download(Archive archive, DTOs.DownloadStates.GoogleDrive state,
        AbsolutePath destination, IJob job, CancellationToken token)
    {
        var msg = await ToMessage(state, true, token);
        return await _downloader.Download(msg!, destination, job, token);
    }

    public override async Task<bool> Verify(Archive archive, DTOs.DownloadStates.GoogleDrive state,
        IJob job, CancellationToken token)
    {
        var result = await ToMessage(state, false, token);
        return result != null;
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.GoogleDrive state)
    {
        return new[] { $"directURL=https://drive.google.com/uc?id={state.Id}&export=download" };
    }

    private async Task<HttpRequestMessage?> ToMessage(DTOs.DownloadStates.GoogleDrive state, bool download,
        CancellationToken token)
    {
        if (download)
        {
            var initialUrl = $"https://drive.google.com/uc?id={state.Id}&export=download";
            var msg = new HttpRequestMessage(HttpMethod.Get, initialUrl);
            msg.AddChromeAgent();

            using var response = await _client.SendAsync(msg, token);
            var cookies = response.GetSetCookies();
            var warning = cookies.FirstOrDefault(c => c.Key.StartsWith("download_warning_"));

            if (warning == default && response.Content.Headers.ContentType?.MediaType == "text/html")
            {
                var doc = new HtmlDocument();
                var txt = await response.Content.ReadAsStringAsync(token);
                if (txt.Contains("<title>Google Drive - Quota exceeded</title>"))
                    throw new Exception("Google Drive - Quota Exceeded");

                doc.LoadHtml(txt);

                var action = doc.DocumentNode.DescendantsAndSelf()
                    .Where(d => d.Name == "form" && d.Id == "downloadForm" &&
                                d.GetAttributeValue("method", "") == "post")
                    .Select(d => d.GetAttributeValue("action", ""))
                    .FirstOrDefault();

                if (action != null)
                    warning = ("download_warning_", "t");

            }
            response.Dispose();
            if (warning == default)
            {
                return new HttpRequestMessage(HttpMethod.Get, initialUrl);
            }

            var url = $"https://drive.google.com/uc?export=download&confirm={warning.Value}&id={state.Id}";
            var httpState = new HttpRequestMessage(HttpMethod.Get, url);
            httpState.AddChromeAgent();
            return httpState;
        }
        else
        {
            var url = $"https://drive.google.com/file/d/{state.Id}/edit";
            var msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.AddChromeAgent();

            using var response = await _client.SendAsync(msg, token);
            msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.AddChromeAgent();
            return !response.IsSuccessStatusCode ? null : msg;
        }
    }
}