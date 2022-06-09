using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.ModDB;

public class ModDBDownloader : ADownloader<DTOs.DownloadStates.ModDB>, IUrlDownloader, IProxyable
{
    private readonly IHttpDownloader _downloader;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModDBDownloader> _logger;

    public ModDBDownloader(ILogger<ModDBDownloader> logger, HttpClient httpClient, IHttpDownloader downloader)
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
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") &&
            iniData["directURL"].StartsWith("https://www.moddb.com/downloads/start") &&
            Uri.TryCreate(iniData["directURL"], UriKind.Absolute, out var uri))
        {
            var state = new DTOs.DownloadStates.ModDB
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
        if (!uri.ToString().StartsWith("https://www.moddb.com/downloads/start"))
            return null;
        return new DTOs.DownloadStates.ModDB {Url = uri};
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((DTOs.DownloadStates.ModDB) state).Url;
    }

    public async Task<T> DownloadStream<T>(Archive archive, Func<Stream, Task<T>> fn, CancellationToken token)
    {
        var state = archive.State as DTOs.DownloadStates.ModDB;
        var url = (await GetDownloadUrls(state!)).First();
        try
        {
            var msg = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };
            using var response = await _httpClient.SendAsync(msg, token);
            HttpException.ThrowOnFailure(response);
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            return await fn(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While downloading from ModDB");
            throw;
        }
    }

    public override async Task<Hash> Download(Archive archive, DTOs.DownloadStates.ModDB state,
        AbsolutePath destination, IJob job, CancellationToken token)
    {
        var urls = await GetDownloadUrls(state);
        foreach (var (url, idx) in urls.Zip(Enumerable.Range(0, urls.Length), (s, i) => (s, i)))
            try
            {
                var msg = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(url)
                };
                return await _downloader.Download(msg, destination, job, token);
            }
            catch (Exception)
            {
                if (idx == urls.Length - 1)
                    throw;
                _logger.LogInformation("Download from {url} failed, trying next mirror", url);
            }

        return default;
    }

    private async Task<string[]> GetDownloadUrls(DTOs.DownloadStates.ModDB state, CancellationToken? token = null)
    {
        var modId = state.Url.AbsolutePath.Split('/').Reverse().FirstOrDefault(f => int.TryParse(f, out _));
        if (modId == default)
            return Array.Empty<string>();

        var data = await _httpClient.GetStringAsync($"https://www.moddb.com/downloads/start/{modId}/all",
            token ?? CancellationToken.None);
        var doc = new HtmlDocument();
        doc.LoadHtml(data);
        var mirrors = doc.DocumentNode.Descendants().Where(d => d.NodeType == HtmlNodeType.Element && d.HasClass("row"))
            .Select(d => new
            {
                Link = "https://www.moddb.com" +
                       d.Descendants().Where(s => s.Id == "downloadon")
                           .Select(i => i.GetAttributeValue("href", ""))
                           .FirstOrDefault(),
                Load = d.Descendants().Where(s => s.HasClass("subheading"))
                    .Select(i => i.InnerHtml.Split(',')
                        .Last()
                        .Split('%')
                        .Select(v => double.TryParse(v, out var dr) ? dr : double.MaxValue)
                        .First())
                    .FirstOrDefault()
            })
            .OrderBy(d => d.Load)
            .ToList();

        return mirrors.Select(d => d.Link).ToArray();
    }

    public override async Task<bool> Verify(Archive archive, DTOs.DownloadStates.ModDB archiveState, IJob job,
        CancellationToken token)
    {
        var urls = await GetDownloadUrls(archiveState, token);
        return urls.Any();
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.ModDB state)
    {
        return new[] {$"directURL={state.Url}"};
    }
}