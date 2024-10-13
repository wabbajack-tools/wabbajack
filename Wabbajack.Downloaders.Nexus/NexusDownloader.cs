using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders;

public class NexusDownloader : ADownloader<Nexus>, IUrlDownloader
{
    private readonly NexusApi _api;
    private readonly HttpClient _client;
    private readonly IHttpDownloader _downloader;
    private readonly ILogger<NexusDownloader> _logger;
    private readonly IUserInterventionHandler _userInterventionHandler;
    private readonly IResource<IUserInterventionHandler> _interventionLimiter;

    private const bool IsManualDebugMode = false;

    public NexusDownloader(ILogger<NexusDownloader> logger, HttpClient client, IHttpDownloader downloader,
        NexusApi api, IUserInterventionHandler userInterventionHandler, IResource<IUserInterventionHandler> interventionLimiter)
    {
        _logger = logger;
        _client = client;
        _downloader = downloader;
        _api = api;
        _userInterventionHandler = userInterventionHandler;
        _interventionLimiter = interventionLimiter;
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(_api.AuthInfo.HaveToken());
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public IDownloadState? Parse(Uri uri)
    {
        if (uri.Host != "www.nexusmods.com")
            return null;
        var relPath = (RelativePath) uri.AbsolutePath;
        long modId, fileId;

        if (relPath.Depth != 3)
        {
            _logger.LogWarning("Got www.nexusmods.com link but it didn't match a parsable pattern: {url}", uri);
            return null;
        }

        if (!long.TryParse(relPath.FileName.ToString(), out modId))
            return null;

        var game = GameRegistry.ByNexusName[relPath.Parent.Parent.ToString()].FirstOrDefault();
        if (game == null) return null;

        var query = HttpUtility.ParseQueryString(uri.Query);
        var fileIdStr = query.Get("file_id");
        if (!long.TryParse(fileIdStr, out fileId))
            return null;

        return new Nexus
        {
            Game = game.Game,
            ModID = modId,
            FileID = fileId
        };
    }

    public Uri UnParse(IDownloadState state)
    {
        var nstate = (Nexus) state;
        return new Uri(
            $"https://www.nexusmods.com/{nstate.Game.MetaData().NexusName}/mods/{nstate.ModID}/?tab=files&file_id={nstate.FileID}");
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.TryGetValue("gameName", out var gameName) &&
            iniData.TryGetValue("modID", out var modId) &&
            iniData.TryGetValue("fileID", out var fileId) &&
            !string.IsNullOrWhiteSpace(gameName) &&
            !string.IsNullOrWhiteSpace(modId) &&
            !string.IsNullOrWhiteSpace(fileId))
        {
            var gameMetaData = GameRegistry.GetByMO2ArchiveName(gameName) ?? GameRegistry.GetByNexusName(gameName);
            return new Nexus
            {
                Game = gameMetaData!.Game,
                ModID = long.Parse(modId),
                FileID = long.Parse(fileId)
            };
        }

        return null;
    }

    public override Priority Priority => Priority.Normal;

    public override async Task<Hash> Download(Archive archive, Nexus state, AbsolutePath destination,
        IJob job, CancellationToken token)
    {
        if (IsManualDebugMode || !(await _api.IsPremium(token)))
        {
            return await DownloadManually(archive, state, destination, job, token);
        }
        else
        {
            try
            {
                var urls = await _api.DownloadLink(state.Game.MetaData().NexusName!, state.ModID, state.FileID, token);
                _logger.LogInformation("Downloading Nexus File: {game}|{modid}|{fileid}", state.Game, state.ModID,
                    state.FileID);
                foreach (var link in urls.info)
                {
                    if (token.IsCancellationRequested)
                    {
                        return new Hash();
                    }

                    try
                    {
                        var message = new HttpRequestMessage(HttpMethod.Get, link.URI);
                        return await _downloader.Download(message, destination, job, token);
                    }
                    catch (Exception ex)
                    {
                        if (link.URI == urls.info.Last().URI)
                            throw;
                        _logger.LogInformation(ex, "While downloading {URI}, trying another link", link.URI);
                    }
                }

                // Should never be hit
                throw new NotImplementedException();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "While downloading from the Nexus {Message}", ex.Message);
                if (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    return await DownloadManually(archive, state, destination, job, token);
                }

                throw;
            }
        }
    }

    private async Task<Hash> DownloadManually(Archive archive, Nexus state, AbsolutePath destination, IJob job, CancellationToken token)
    {
        var md = new ManualDownload(new Archive
        {
            Name = archive.Name,
            Hash = archive.Hash,
            Meta = archive.Meta,
            Size = archive.Size,
            State = new Manual
            {
                Prompt = "Click Download - Buy Nexus Premium to automate this process",
                Url = new Uri($"https://www.nexusmods.com/{state.Game.MetaData().NexusName}/mods/{state.ModID}?tab=files&file_id={state.FileID}")
            }
        });

        ManualDownload.BrowserDownloadState browserState;
        using (var _ = await _interventionLimiter.Begin("Downloading file manually", 1, token))
        {
            _userInterventionHandler.Raise(md);
            browserState = await md.Task;
        }

        
        var msg = browserState.ToHttpRequestMessage();
        
        using var response = await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead,  token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(response.ReasonPhrase, null, statusCode:response.StatusCode);

        await using var strm = await response.Content.ReadAsStreamAsync(token);
        await using var os = destination.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        return await strm.HashingCopy(os, token, job);
    }

    public override async Task<bool> Verify(Archive archive, Nexus state, IJob job, CancellationToken token)
    {
        try
        {
            var fileInfo = await _api.FileInfo(state.Game.MetaData().NexusName!, state.ModID, state.FileID, token);
            var (modInfo, _) = await _api.ModInfo(state.Game.MetaData().NexusName!, state.ModID, token);

            state.Description = FixupSummary(modInfo.Summary);
            state.Version = modInfo.Version;
            state.Author = modInfo.Author;
            if (Uri.TryCreate(modInfo.PictureUrl, UriKind.Absolute, out var uri)) 
                state.ImageURL = uri;
            state.Name = modInfo.Name;
            state.IsNSFW = modInfo.ContainsAdultContent;
            
            
            return fileInfo.info.FileId == state.FileID;
        }
        catch (HttpException ex)
        {
            _logger.LogError($"HttpException: {ex} on {archive.Name}");
            return false;
        }
    }
    
    public static string FixupSummary(string? argSummary)
    {
        if (argSummary == null)
            return "";
        return argSummary.Replace("&#39;", "'")
            .Replace("<br/>", "\n\n")
            .Replace("<br />", "\n\n")
            .Replace("&#33;", "!");
    }

    public override IEnumerable<string> MetaIni(Archive a, Nexus state)
    {
        var meta = state.Game.MetaData();
        return new[]
        {
            $"gameName={meta.MO2ArchiveName ?? meta.NexusName}", $"modID={state.ModID}", $"fileID={state.FileID}"
        };
    }
}