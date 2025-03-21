using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.Manual;

public class ManualDownloader : ADownloader<DTOs.DownloadStates.Manual>, IProxyable
{
    private readonly ILogger<ManualDownloader> _logger;
    private readonly IUserInterventionHandler _interventionHandler;
    private readonly IHttpDownloader _downloader;

    public ManualDownloader(ILogger<ManualDownloader> logger, IUserInterventionHandler interventionHandler, IHttpDownloader downloader)
    {
        _logger = logger;
        _interventionHandler = interventionHandler;
        _downloader = downloader;
    }

    public override Task<Hash> Download(Archive archive, DTOs.DownloadStates.Manual state, AbsolutePath destination, IJob job, CancellationToken token)
    {
        _logger.LogInformation("Starting manual download of {Url}", state.Url);

        if (ShouldUseBlobDownload(state.Url))
        {
            return DoManualBlobDownload(archive, destination, token);
        }
        else
        {
            return DoManualDownload(archive, destination, job, token);
        }
    }

    private bool ShouldUseBlobDownload(Uri url)
    {
        return url.Host.EndsWith("loverslab.com");
    }

    private async Task<Hash> DoManualDownload(Archive archive, AbsolutePath destination, IJob job, CancellationToken token)
    {
        var intervention = new ManualDownload(archive);
        _interventionHandler.Raise(intervention);
        var browserState = await intervention.Task;

        var msg = browserState.ToHttpRequestMessage();
        return await _downloader.Download(msg, destination, job, token);
    }

    private async Task<Hash> DoManualBlobDownload(Archive archive, AbsolutePath destination, CancellationToken token)
    {
        var intervention = new ManualBlobDownload(archive, destination);
        _interventionHandler.Raise(intervention);
        await intervention.Task;

        await using var file = destination.Open(FileMode.Open);
        return await file.Hash(token);
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return allowList.AllowedPrefixes.Any(p => ((DTOs.DownloadStates.Manual)state).Url.ToString().StartsWith(p));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("manualURL") && Uri.TryCreate(iniData["manualURL"].CleanIniString(), UriKind.Absolute, out var uri))
        {
            iniData.TryGetValue("prompt", out var prompt);

            var state = new DTOs.DownloadStates.Manual
            {
                Url = uri,
                Prompt = prompt ?? ""
            };

            return state;
        }

        return null;
    }

    public override Priority Priority { get; } = Priority.Lowest;
    public override Task<bool> Verify(Archive archive, DTOs.DownloadStates.Manual archiveState, IJob job, CancellationToken token)
    {
        return Task.FromResult(true);
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.Manual state)
    {

        return new[] { $"manualURL={state.Url}", $"prompt={state.Prompt}" };
    }

    public IDownloadState? Parse(Uri uri)
    {
        return new DTOs.DownloadStates.Manual() { Url = uri };
    }

    public Uri UnParse(IDownloadState state)
    {
        return (state as DTOs.DownloadStates.Manual)!.Url;
    }

    public Task<T> DownloadStream<T>(Archive archive, Func<Stream, Task<T>> fn, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}