using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CefSharp;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.WebAutomation;

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
            CancellationTokenSource cancel = new CancellationTokenSource();
            var oldPane = MainWindow.ActivePane;
            var vm = await WebBrowserVM.GetNew();
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

        public async Task Handle(IUserIntervention msg)
        {
            switch (msg)
            {
                case RequestNexusAuthorization c:
                    await WrapBrowserJob(msg, async (vm, cancel) =>
                    {
                        await vm.Driver.WaitForInitialized();
                        var key = await NexusApiClient.SetupNexusLogin(new CefSharpWrapper(vm.Browser), m => vm.Instructions = m, cancel.Token);
                        c.Resume(key);
                    });
                    break;
                case ManuallyDownloadNexusFile c:
                    await WrapBrowserJob(msg, (vm, cancel) => HandleManualNexusDownload(vm, cancel, c));
                    break;
                case RequestBethesdaNetLogin c:
                    var data = await BethesdaNetDownloader.Login();
                    c.Resume(data);
                    break;
                case AbstractNeedsLoginDownloader.RequestSiteLogin c:
                    await WrapBrowserJob(msg, async (vm, cancel) =>
                    {
                        await vm.Driver.WaitForInitialized();
                        var data = await c.Downloader.GetAndCacheCookies(new CefSharpWrapper(vm.Browser), m => vm.Instructions = m, cancel.Token);
                        c.Resume(data);
                    });
                    break;
                case CriticalFailureIntervention c:
                    MessageBox.Show(c.ExtendedDescription, c.ShortDescription, MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    c.Cancel();
                    break;
                case ConfirmationIntervention c:
                    break;
                default:
                    throw new NotImplementedException($"No handler for {msg}");
            }
        }

        private async Task HandleManualNexusDownload(WebBrowserVM vm, CancellationTokenSource cancel, ManuallyDownloadNexusFile manuallyDownloadNexusFile)
        {
            var state = manuallyDownloadNexusFile.State;
            var game = GameRegistry.GetByMO2ArchiveName(state.GameName);
            var hrefs = new[]
            {
                $"/Core/Libs/Common/Widgets/DownloadPopUp?id={state.FileID}&game_id={game.NexusGameId}",
                $"https://www.nexusmods.com/{game.NexusName}/mods/{state.ModID}?tab=files&file_id={state.FileID}",
                $"/Core/Libs/Common/Widgets/ModRequirementsPopUp?id={state.FileID}&game_id={game.NexusGameId}"
            };
            await vm.Driver.WaitForInitialized();
            IWebDriver browser = new CefSharpWrapper(vm.Browser);
            vm.Instructions = $"Please Download {state.ModName} - {state.ModID} - {state.FileID}";
            browser.DownloadHandler = uri =>
            {
                manuallyDownloadNexusFile.Resume(uri);
            };
            await browser.NavigateTo(NexusApiClient.ManualDownloadUrl(manuallyDownloadNexusFile.State));

            var buttin_href = $"/Core/Libs/Common/Widgets/DownloadPopUp?id={manuallyDownloadNexusFile.State.FileID}&game_id={Game.SkyrimSpecialEdition}";

            while (!cancel.IsCancellationRequested && !manuallyDownloadNexusFile.Task.IsCompleted) {
                await browser.EvaluateJavaScript(
                    @"Array.from(document.getElementsByClassName('accordion')).forEach(e => Array.from(e.children).forEach(c => c.style=''))");
                foreach (var href in hrefs)
                {
                    const string style = "border-thickness: thick; border-color: #ff0000;border-width: medium;border-style: dashed;background-color: teal;padding: 7px";
                    await browser.EvaluateJavaScript($"Array.from(document.querySelectorAll('.accordion a[href=\"{href}\"]')).forEach(e => {{e.scrollIntoView({{behavior: 'smooth', block: 'center', inline: 'nearest'}}); e.setAttribute('style', '{style}');}});");
                }
                await Task.Delay(250);
                
            }
        }
    }
}
