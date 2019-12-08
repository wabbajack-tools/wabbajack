using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.StatusMessages;

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
            var vm = new WebBrowserVM();
            MainWindow.ActivePane = vm;
            vm.BackCommand = ReactiveCommand.Create(() =>
            {
                cancel.Cancel();
                MainWindow.ActivePane = oldPane;
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

            MainWindow.ActivePane = oldPane;
        }

        public void Handle(ConfirmUpdateOfExistingInstall msg)
        {
            var result = MessageBox.Show(msg.ExtendedDescription, msg.ShortDescription, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
                msg.Confirm();
            else
                msg.Cancel();
        }

        public async Task Handle(IUserIntervention msg)
        {
            switch (msg)
            {
                case ConfirmUpdateOfExistingInstall c: 
                    Handle(c);
                    break;
                case RequestNexusAuthorization c:
                    await WrapBrowserJob(msg, async (vm, cancel) =>
                    {
                        var key = await NexusApiClient.SetupNexusLogin(vm.Browser, m => vm.Instructions = m, cancel.Token);
                        c.Resume(key);
                    });
                    break;
                case RequestLoversLabLogin c:
                    await WrapBrowserJob(msg, async (vm, cancel) =>
                    {
                        var data = await LoversLabDownloader.GetAndCacheLoversLabCookies(vm.Browser, m => vm.Instructions = m, cancel.Token);
                        c.Resume(data);
                    });
                    break;
                default:
                    throw new NotImplementedException($"No handler for {msg}");
            }
        }
    }
}
