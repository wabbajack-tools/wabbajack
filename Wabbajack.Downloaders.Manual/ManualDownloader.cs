using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.Manual;

public class ManualDownloader : ADownloader<DTOs.DownloadStates.Manual>, IProxyable
{
    private readonly ILogger<ManualDownloader> _logger;
    private readonly IUserInterventionHandler _interventionHandler;
    private readonly IResource<HttpClient> _limiter;
    private readonly HttpClient _client;

    public ManualDownloader(ILogger<ManualDownloader> logger, IUserInterventionHandler interventionHandler, HttpClient client)
    {
        _logger = logger;
        _interventionHandler = interventionHandler;
        _client = client;
    }
    
    public override async Task<Hash> Download(Archive archive, DTOs.DownloadStates.Manual state, AbsolutePath destination, IJob job, CancellationToken token)
    {
        _logger.LogInformation("Starting manual download of {Url}", state.Url);

        if (state.Url.Host == "mega.nz")
        {
            var intervention = new ManualBlobDownload(archive, destination);
            _interventionHandler.Raise(intervention);
            await intervention.Task;
            if (!destination.FileExists())
                throw new Exception("File does not exist after download");
            _logger.LogInformation("Hashing manually downloaded Mega file {File}", destination.FileName);
            return await destination.Hash(token);
        }
        else
        {
            var intervention = new ManualDownload(archive);
            _interventionHandler.Raise(intervention);
            var browserState = await intervention.Task;

            var msg = browserState.ToHttpRequestMessage();

            using var response = await _client.SendAsync(msg, token);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(response.ReasonPhrase, null, statusCode: response.StatusCode);

            await using var strm = await response.Content.ReadAsStreamAsync(token);
            await using var os = destination.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            return await strm.HashingCopy(os, token, job);
        }
    }


    public override async Task<bool> Prepare()
    {
        return true;
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return allowList.AllowedPrefixes.Any(p => ((DTOs.DownloadStates.Manual) state).Url.ToString().StartsWith(p));
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
    public override async Task<bool> Verify(Archive archive, DTOs.DownloadStates.Manual archiveState, IJob job, CancellationToken token)
    {
        return true;
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.Manual state)
    {

        return new[] {$"manualURL={state.Url}", $"prompt={state.Prompt}"};
    }

    public IDownloadState? Parse(Uri uri)
    {
        return new DTOs.DownloadStates.Manual() {Url = uri};
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