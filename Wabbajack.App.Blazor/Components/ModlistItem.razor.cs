using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Wabbajack.App.Blazor.Store;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;


namespace Wabbajack.App.Blazor.Components
{
    public partial class ModlistItem
    {
        [Inject] private IState<DownloadState>     _downloadState    { get; set; }
        [Inject] private IState<InstallState>      _installState     { get; set; }
        [Inject] private ModListDownloadMaintainer _maintainer       { get; set; }
        [Inject] private IDispatcher               _dispatcher       { get; set; }
        [Inject] private NavigationManager         NavigationManager { get; set; }

        [Parameter] public ModlistMetadata Metadata { get; set; }
        
        public Percent DownloadProgress { get; set; }

        private async Task Download()
        {
            await using Timer timer = new(_ => InvokeAsync(StateHasChanged));
            timer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
            try
            {
                UpdateDownloadState(DownloadState.DownloadStateEnum.Downloading, Metadata);
                (IObservable<Percent> progress, Task task) = _maintainer.DownloadModlist(Metadata);
                IDisposable dispose = progress.Subscribe(p => DownloadProgress = p);

                await task;
                //await _wjClient.SendMetric("downloading", Metadata.Title);
                UpdateDownloadState(DownloadState.DownloadStateEnum.Downloaded, Metadata);
                dispose.Dispose();
                
                AbsolutePath path = KnownFolders.EntryPoint.Combine("downloaded_mod_lists", Metadata.Links.MachineURL).WithExtension(Ext.Wabbajack);
                _dispatcher.Dispatch(new UpdateInstallState(InstallState.InstallStateEnum.Configuration, null, path, null, null));
                NavigationManager.NavigateTo("/configure");

            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                UpdateDownloadState(DownloadState.DownloadStateEnum.Failure, Metadata);
            }

            await timer.DisposeAsync();
        }

        private void UpdateDownloadState(DownloadState.DownloadStateEnum state, ModlistMetadata metadata) =>
            _dispatcher.Dispatch(new UpdateDownloadState(state, metadata));
    }
}