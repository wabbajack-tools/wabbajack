using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Blazor.Components
{
    public partial class ModlistItem
    {
        //[Inject] private ILogger                   _logger     { get; set; }
        [Inject] private ModListDownloadMaintainer _maintainer { get; set; }
        //[Inject] private Client                    _wjClient   { get; set; }

        [Parameter] public ModlistMetadata Metadata { get; set; }

        public  DownloadStatus      Status            { get; set; }
        public  double             PercentDownloaded { get; set; }
        private CompositeDisposable _disposables      { get; set; }

        private async Task Download()
        {
            await using Timer timer = new(_ => InvokeAsync(StateHasChanged));
            timer.Change(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
            try
            {
                _disposables = new CompositeDisposable();
                Status       = DownloadStatus.Downloading;

                (IObservable<Percent> progress, Task task) = _maintainer.DownloadModlist(Metadata);
                IDisposable dispose = progress.Subscribe(p =>
                {
                    PercentDownloaded = p.Value * 100;
                });

                await task;
                //await _wjClient.SendMetric("downloading", Metadata.Title);
                //await UpdateStatus();
                Debug.Print("##### WE DOWNLOADED THE THING!");
                dispose.Dispose();
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }

            await timer.DisposeAsync();
        }
        
        public enum DownloadStatus
        {
            NotDownloaded,
            Downloading,
            Downloaded
        }
    }
}