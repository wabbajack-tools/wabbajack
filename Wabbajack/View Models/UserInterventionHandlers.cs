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

        public UserInterventionHandlers(MainWindowVM mvm)
        {
            MainWindow = mvm;
        }

        private async Task WrapBrowserJob(IUserIntervention intervention, Func<WebBrowserVM, CancellationTokenSource, Task> toDo)
        {
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


            Helpers.SchemeHandler = (browser, frame, _, request) =>
            {
                var req = new Uri(request.Url);
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
                    oa.Resume(parsed.Get("code"));
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
            vm.Instructions = $"Please locate and download {manuallyDownloadFile.State.Url}";

            var result = new TaskCompletionSource<Uri>();

            browser.DownloadHandler = uri =>
            {
                //var client = Helpers.GetClient(browser.GetCookies("").Result, browser.Location);
                result.SetResult(uri);
            };
            
            await vm.Driver.WaitForInitialized();

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

        private async Task HandleManualNexusDownload(WebBrowserVM vm, CancellationTokenSource cancel, ManuallyDownloadNexusFile manuallyDownloadNexusFile)
        {
            var state = manuallyDownloadNexusFile.State;
            var game = state.Game.MetaData();
            await vm.Driver.WaitForInitialized();
            IWebDriver browser = new CefSharpWrapper(vm.Browser);
            vm.Instructions = $"Click the download button to continue (get a NexusMods.com Premium account to automate this)";
            browser.DownloadHandler = uri =>
            {
                manuallyDownloadNexusFile.Resume(uri);
                browser.DownloadHandler = null;
            };
            var url = new Uri(@$"https://www.nexusmods.com/{game.NexusName}/mods/{state.ModID}?tab=files&file_id={state.FileID}");
            await browser.NavigateTo(url);
            
            while (!cancel.IsCancellationRequested && !manuallyDownloadNexusFile.Task.IsCompleted) {
                await Task.Delay(250);
            }
        }
    }
}
