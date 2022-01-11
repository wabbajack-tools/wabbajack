using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Fluxor;
using Wabbajack.App.Blazor.Store;
using Wabbajack.DTOs;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Blazor.Controllers;

public class DownloadController
{
    private readonly ModListDownloadMaintainer _maintainer;
    private readonly IState<DownloadState>     _downloadState;
    private          IDispatcher               _dispatcher;

    public DownloadController(ModListDownloadMaintainer maintainer, IState<DownloadState> downloadState, IDispatcher dispatcher)
    {
        _maintainer    = maintainer;
        _downloadState = downloadState;
        _dispatcher    = dispatcher;
    }

    private async Task DownloadModlist(ModlistMetadata metadata)
    {
        // await using Timer timer = new(_ => InvokeAsync(StateHasChanged));
        // timer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        try
        {
            (IObservable<Percent> progress, Task task) = _maintainer.DownloadModlist(metadata);
            IDisposable dispose = progress.Subscribe(p =>
            {
                UpdateDownloadState(DownloadState.DownloadStateEnum.Downloading, metadata, p);
            });

            await task;
            //await _wjClient.SendMetric("downloading", Metadata.Title);
            UpdateDownloadState(DownloadState.DownloadStateEnum.Downloaded, metadata);
            dispose.Dispose();
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
        }

        // await timer.DisposeAsync();
    }
    
    private void UpdateDownloadState(DownloadState.DownloadStateEnum state, ModlistMetadata metadata, Percent? progress = null) => _dispatcher.Dispatch(new UpdateDownloadState(state, metadata, progress));
}