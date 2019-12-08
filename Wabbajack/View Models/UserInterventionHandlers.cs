using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ReactiveUI;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.StatusMessages;

namespace Wabbajack
{
    public class UserInterventionHandlers
    {
        public Dispatcher ViewDispatcher { get; set; }
        public MainWindowVM MainWindow { get; set; }
        internal void Handle(RequestLoversLabLogin msg)
        {
            ViewDispatcher.InvokeAsync(async () =>
            {
                var oldPane = MainWindow.ActivePane;
                var vm = new WebBrowserVM();
                MainWindow.ActivePane = vm;
                try
                {
                    vm.BackCommand = ReactiveCommand.Create(() =>
                    {
                        MainWindow.ActivePane = oldPane;
                        msg.Cancel();
                    });
                }
                catch (Exception e)
                { }

                try
                {
                    var data = await LoversLabDownloader.GetAndCacheLoversLabCookies(vm.Browser, m => vm.Instructions = m);
                    msg.Resume(data);
                }
                catch (Exception ex)
                {
                    msg.Cancel();
                }
                MainWindow.ActivePane = oldPane;

            });
        }

        internal void Handle(RequestNexusAuthorization msg)
        {
            ViewDispatcher.InvokeAsync(async () =>
            {
                var oldPane = MainWindow.ActivePane;
                var vm = new WebBrowserVM();
                MainWindow.ActivePane = vm;
                try
                {
                    vm.BackCommand = ReactiveCommand.Create(() =>
                    {
                        MainWindow.ActivePane = oldPane;
                        msg.Cancel();
                    });
                }
                catch (Exception e)
                { }

                try
                {
                    var key = await NexusApiClient.SetupNexusLogin(vm.Browser, m => vm.Instructions = m);
                    msg.Resume(key);
                }
                catch (Exception ex)
                {
                    msg.Cancel();
                }
                MainWindow.ActivePane = oldPane;

            });
        }

        internal void Handle(ConfirmUpdateOfExistingInstall msg)
        {
            var result = MessageBox.Show(msg.ExtendedDescription, msg.ShortDescription, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
                msg.Confirm();
            else
                msg.Cancel();
        }

        public void Handle(IUserIntervention msg)
        {
            switch (msg)
            {
                case ConfirmUpdateOfExistingInstall c: 
                    Handle(c);
                    break;
                case RequestNexusAuthorization c:
                    Handle(c);
                    break;
                case RequestLoversLabLogin c:
                    Handle(c);
                    break;
                default:
                    throw new NotImplementedException($"No handler for {msg}");
            }
        }
    }
}
