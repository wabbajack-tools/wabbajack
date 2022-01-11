using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Blazor.Store;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Blazor.Components
{
    public partial class ModlistItem
    {
        [Inject] private ModListDownloadMaintainer _maintainer    { get; set; }
        [Inject] private IState<DownloadState>     _downloadState { get; set; }
        [Inject] private IDispatcher               _dispatcher    { get; set; }

        [Parameter] public ModlistMetadata Metadata { get; set; }
        
        public double         PercentDownloaded { get; set; }

        private async Task Download()
        {
            await using Timer timer = new(_ => InvokeAsync(StateHasChanged));
            timer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
            try
            {
                UpdateInstallState(InstallState.InstallStateEnum.Downloading, Metadata);

                (IObservable<Percent> progress, Task task) = _maintainer.DownloadModlist(Metadata);
                IDisposable dispose = progress.Subscribe(p => { PercentDownloaded = p.Value * 100; });

                await task;
                //await _wjClient.SendMetric("downloading", Metadata.Title);
                Debug.Print("##### WE DOWNLOADED THE THING!");
                UpdateInstallState(InstallState.InstallStateEnum.Configuration);
                dispose.Dispose();
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }

            await timer.DisposeAsync();
        }

        private void UpdateInstallState(InstallState.InstallStateEnum state, ModlistMetadata? metadata = null) => _dispatcher.Dispatch(new UpdateInstallState(state, metadata));
    }
}