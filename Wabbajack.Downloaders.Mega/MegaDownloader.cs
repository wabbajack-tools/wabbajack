using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders;

public class MegaDownloader : ADownloader<Mega>, IUrlDownloader, IProxyable
{
    private const string MegaPrefix = "https://mega.nz/#!";
    private const string MegaFilePrefix = "https://mega.nz/file/";
    private readonly MegaApiClient _apiClient;
    private readonly ILogger<MegaDownloader> _logger;
    private readonly ITokenProvider<MegaToken> _tokenProvider;

    public MegaDownloader(ILogger<MegaDownloader> logger, MegaApiClient apiClient, ITokenProvider<MegaToken> tokenProvider)
    {
        _logger = logger;
        _apiClient = apiClient;
        _tokenProvider = tokenProvider;
    }

    public override async Task<bool> Prepare()
    {
        return await LoginIfNotLoggedIn();
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        var megaState = (Mega) state;
        return allowList.AllowedPrefixes.Any(p => megaState.Url.ToString().StartsWith(p));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        return iniData.ContainsKey("directURL") ? GetDownloaderState(iniData["directURL"].CleanIniString()) : null;
    }

    public override Priority Priority => Priority.Normal;

    public IDownloadState? Parse(Uri uri)
    {
        return GetDownloaderState(uri.ToString());
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((Mega) state).Url;
    }

    public async Task<T> DownloadStream<T>(Archive archive, Func<Stream, Task<T>> fn, CancellationToken token)
    {
        var state = archive.State as Mega;
        await LoginIfNotLoggedIn();

        await using var ins = await _apiClient.DownloadAsync(state!.Url, cancellationToken: token);
        return await fn(ins);
    }

    private async Task<bool> LoginIfNotLoggedIn()
    {
        if (!_apiClient.IsLoggedIn)
        {
            if (_tokenProvider.HaveToken())
            {
                var authInfo = await _tokenProvider.Get();
                try
                {
                    if (authInfo!.Login == null)
                        await _apiClient.LoginAnonymousAsync();
                    else
                        await _apiClient.LoginAsync(authInfo!.Login);
                }
                catch(Exception ex)
                {
                    _logger.LogError("Failed to login to MEGA using provided credentials: {ex}", ex.ToString());
                    return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    public override async Task<Hash> Download(Archive archive, Mega state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        await LoginIfNotLoggedIn();
        
        await using var ous = destination.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await using var ins = await _apiClient.DownloadAsync(state.Url, cancellationToken: token);
        return await ins.HashingCopy(ous, token, job);
    }

    private Mega? GetDownloaderState(string? url)
    {
        if (url == null) return null;

        if (url.StartsWith(MegaPrefix) || url.StartsWith(MegaFilePrefix))
            return new Mega {Url = new Uri(url)};
        return null;
    }

    public override async Task<bool> Verify(Archive archive, Mega archiveState, IJob job, CancellationToken token)
    {
        await LoginIfNotLoggedIn();
        
        for (var times = 0; times < 5; times++)
        {
            try
            {
                var node = await _apiClient.GetNodeFromLinkAsync(archiveState.Url);
                if (node != null)
                    return true;
            }
            catch (Exception)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), token);
        }

        return false;
    }

    public override IEnumerable<string> MetaIni(Archive a, Mega state)
    {
        return new[] {$"directURL={state.Url}"};
    }
}