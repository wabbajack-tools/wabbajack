using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Threading;
using CefSharp;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.WebAutomation;
using WebSocketSharp;

namespace Wabbajack
{
    public class UserInterventionHandlers
    {
        public MainWindowVM MainWindow { get; }
        private AsyncLock _browserLock = new();

        public UserInterventionHandlers(MainWindowVM mvm)
        {
            MainWindow = mvm;
        }

        private async Task WrapBrowserJob(IUserIntervention intervention, Func<WebBrowserVM, CancellationTokenSource, Task> toDo)
        {
            using var wait = await _browserLock.WaitAsync();
            var cancel = new CancellationTokenSource();
            var oldPane = MainWindow.ActivePane;
            using var vm = await WebBrowserVM.GetNew();
            MainWindow.NavigateTo(vm);
            vm.BackCommand = ReactiveCommand.Create(() =>
            {
                cancel.Cancel();
                MainWindow.NavigateTo(oldPane);
                intervention.Cancel();
            });

            try
            {
                await toDo(vm, cancel);
            }
            catch (TaskCanceledException)
            {
                intervention.Cancel();
            }
            catch (Exception ex)
            {
                Utils.Error(ex);
                intervention.Cancel();
            }

            MainWindow.NavigateTo(oldPane);
        }

        public async Task Handle(IStatusMessage msg)
        {
            switch (msg)
            {
                case RequestNexusAuthorization c:
                    await WrapBrowserJob(c, async (vm, cancel) =>
                    {
                        await vm.Driver.WaitForInitialized();
                        var key = await NexusApiClient.SetupNexusLogin(new CefSharpWrapper(vm.Browser), m => vm.Instructions = m, cancel.Token);
                        c.Resume(key);
                    });
                    break;
                case ManuallyDownloadNexusFile c:
                    await WrapBrowserJob(c, (vm, cancel) => HandleManualNexusDownload(vm, cancel, c));
                    break;
                case ManuallyDownloadFile c:
                    await WrapBrowserJob(c, (vm, cancel) => HandleManualDownload(vm, cancel, c));
                    break;
                case ManuallyDownloadMegaFile c:
                    await WrapBrowserJob(c, (vm, cancel) => HandleManualMegaDownload(vm, cancel, c));
                    break;
                case ManuallyDownloadLoversLabFile c:
                    await WrapBrowserJob(c, (vm, cancel) => HandleManualLoversLabDownload(vm, cancel, c));
                    break;
                case AbstractNeedsLoginDownloader.RequestSiteLogin c:
                    await WrapBrowserJob(c, async (vm, cancel) =>
                    {
                        await vm.Driver.WaitForInitialized();
                        var data = await c.Downloader.GetAndCacheCookies(new CefSharpWrapper(vm.Browser), m => vm.Instructions = m, cancel.Token);
                        c.Resume(data);
                    });
                    break;
                case RequestOAuthLogin oa:
                    await WrapBrowserJob(oa, async (vm, cancel) =>
                    {
                        await OAuthLogin(oa, vm, cancel);
                    });


                    break;
                case CriticalFailureIntervention c:
                    MessageBox.Show(c.ExtendedDescription, c.ShortDescription, MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    c.Cancel();
                    if (c.ExitApplication) await MainWindow.ShutdownApplication();
                    break;
                case ConfirmationIntervention c:
                    break;
                default:
                    throw new NotImplementedException($"No handler for {msg}");
            }
        }

        private async Task OAuthLogin(RequestOAuthLogin oa, WebBrowserVM vm, CancellationTokenSource cancel)
        {
            await vm.Driver.WaitForInitialized();
            vm.Instructions = $"Please log in and allow Wabbajack to access your {oa.SiteName} account";
            
            var wrapper = new CefSharpWrapper(vm.Browser);
            var scopes = string.Join(" ", oa.Scopes);
            var state = Guid.NewGuid().ToString();


            var oldHandler = Helpers.SchemeHandler;
            Helpers.SchemeHandler = (browser, frame, _, request) =>
            {
                var req = new Uri(request.Url);
                Utils.LogStraightToFile($"Got Scheme callback {req}");
                var parsed = HttpUtility.ParseQueryString(req.Query);
                if (parsed.Contains("state"))
                {
                    if (parsed.Get("state") != state)
                    {
                        Utils.Log("Bad OAuth state, state, this shouldn't happen");
                        oa.Cancel();
                        return new ResourceHandler();
                    }
                }
                if (parsed.Contains("code"))
                {
                    Helpers.SchemeHandler = oldHandler;
                    oa.Resume(parsed.Get("code")!).FireAndForget();
                }
                else
                {
                    oa.Cancel();
                }
                return new ResourceHandler();
            };
            
            await wrapper.NavigateTo(new Uri(oa.AuthorizationEndpoint + $"?response_type=code&client_id={oa.ClientID}&state={state}&scope={scopes}"));

            while (!oa.Task.IsCanceled && !oa.Task.IsCompleted && !cancel.IsCancellationRequested)
                await Task.Delay(250);
        }

        private async Task HandleManualDownload(WebBrowserVM vm, CancellationTokenSource cancel, ManuallyDownloadFile manuallyDownloadFile)
        {
            var browser = new CefSharpWrapper(vm.Browser);
            var prompt = manuallyDownloadFile.State.Prompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                prompt = $"Please locate and download {manuallyDownloadFile.State.Url}";
            }

            vm.Instructions = prompt;

            var result = new TaskCompletionSource<Uri>();


            await vm.Driver.WaitForInitialized();
            using var _ = browser.SetDownloadHandler(new ManualDownloadHandler(result));

            await browser.NavigateTo(new Uri(manuallyDownloadFile.State.Url));

            while (!cancel.IsCancellationRequested)
            {
                if (result.Task.IsCompleted)
                {
                    var cookies = await Helpers.GetCookies();
                    var referer = browser.Location;
                    var client = Helpers.GetClient(cookies, referer);
                    manuallyDownloadFile.Resume(result.Task.Result, client);
                    break;
                }
                await Task.Delay(100);
            }

        }

        private class ManualDownloadHandler : IDownloadHandler
        {
            private readonly TaskCompletionSource<Uri> _tcs;

            public ManualDownloadHandler(TaskCompletionSource<Uri> tcs)
            {
                _tcs = tcs;
            }

            public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
                IBeforeDownloadCallback callback)
            {
                _tcs.TrySetResult(new Uri(downloadItem.Url));
            }

            public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
                IDownloadItemCallback callback)
            {
                callback.Cancel();
            }
        }


        private async Task HandleManualMegaDownload(WebBrowserVM vm, CancellationTokenSource cancel, ManuallyDownloadMegaFile manuallyDownloadFile)
        {
            var browser = new CefSharpWrapper(vm.Browser);
            var prompt = manuallyDownloadFile.State.Prompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                prompt = $"Please locate and download {manuallyDownloadFile.State.Url}";
            }

            vm.Instructions = prompt;

            await vm.Driver.WaitForInitialized();
            var tcs = new TaskCompletionSource();
            
            using var _ = browser.SetDownloadHandler(new BlobDownloadHandler(manuallyDownloadFile.Destination, tcs));
            
            await browser.NavigateTo(new Uri(manuallyDownloadFile.State.Url));

            while (!cancel.IsCancellationRequested && !tcs.Task.IsCompleted)
            {
                await Task.Delay(100);
            }
            manuallyDownloadFile.Resume();

        }
        
        private async Task HandleManualLoversLabDownload(WebBrowserVM vm, CancellationTokenSource cancel, ManuallyDownloadLoversLabFile manuallyDownloadFile)
        {
            var browser = new CefSharpWrapper(vm.Browser);
            var prompt = manuallyDownloadFile.State.Prompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                prompt = $"Please locate and download {manuallyDownloadFile.State.Url}";
            }

            vm.Instructions = prompt;

            await vm.Driver.WaitForInitialized();
            var tcs = new TaskCompletionSource();

            using var _ = browser.SetDownloadHandler(new BlobDownloadHandler(manuallyDownloadFile.Destination, tcs, 
                p =>
            {
                vm.Instructions = $"Downloading: {p}";
            }, manuallyDownloadFile.Archive));

            
            await browser.NavigateTo(new Uri(manuallyDownloadFile.State.Url));

            while (!cancel.IsCancellationRequested && !tcs.Task.IsCompleted)
            {
                await browser.EvaluateJavaScript(
                    "Array.from(document.getElementsByClassName('ll_adblock')).forEach(c => c.remove())");
                await Task.Delay(100);
            }
            manuallyDownloadFile.Resume();

        }
        
        private class BlobDownloadHandler : IDownloadHandler
        {
            private readonly AbsolutePath _destination;
            private readonly TaskCompletionSource _tcs;
            private readonly Action<Percent> _progress;
            private Archive _archive;

            public BlobDownloadHandler(AbsolutePath f, TaskCompletionSource tcs, Action<Percent> progress = null, Archive archive = null)
            {
                _progress = progress;
                _destination = f;
                _tcs = tcs;
                _archive = archive;
            }
            public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
                IBeforeDownloadCallback callback)
            {
                callback.Continue(_destination.ToString(), false);
            }

            public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
                IDownloadItemCallback callback)
            {
                if (_archive?.Size != null && _archive?.Size != 0 && downloadItem.TotalBytes != _archive?.Size)
                {
                    _tcs.TrySetCanceled();
                    Utils.Error(
                        $"Download of {_archive!.Name} (from {downloadItem.OriginalUrl}) aborted, selected file was {downloadItem.TotalBytes.ToFileSizeString()} expected size was {_archive!.Size.ToFileSizeString()}");
                    callback.Cancel();
                    return;
                }
                
                _progress?.Invoke(Percent.FactoryPutInRange(downloadItem.PercentComplete, 100));
                
                if (downloadItem.IsComplete)
                {
                    _tcs.TrySetResult();
                }
                callback.Resume();
   }
        }

        private async Task HandleManualNexusDownload(WebBrowserVM vm, CancellationTokenSource cancel, ManuallyDownloadNexusFile manuallyDownloadNexusFile)
        {
            var state = manuallyDownloadNexusFile.State;
            var game = state.Game.MetaData();
            await vm.Driver.WaitForInitialized();
            vm.Instructions = $"Click the download button to continue (get a NexusMods.com Premium account to automate this)";
            var browser = new CefSharpWrapper(vm.Browser);
            var tcs = new TaskCompletionSource<Uri>();
            using var _ = browser.SetDownloadHandler(new ManualDownloadHandler(tcs));

            var url = new Uri(@$"https://www.nexusmods.com/{game.NexusName}/mods/{state.ModID}?tab=files&file_id={state.FileID}");
            await browser.NavigateTo(url);
            
            while (!cancel.IsCancellationRequested && !tcs.Task.IsCompleted) {
                await Task.Delay(250);
            }

            if (tcs.Task.IsFaulted)
            {
                manuallyDownloadNexusFile.Cancel();
            }
            else
            {
                var uri = await tcs.Task;
                manuallyDownloadNexusFile.Resume(uri);
            }
        }
    }
}
