using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack;
using Wabbajack.Messages;
using Wabbajack.Paths.IO;
using Wabbajack.Networking.WabbajackClientApi;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Diagnostics;

namespace Wabbajack
{
    public class HomeVM : ViewModel
    {
        private readonly ILogger<HomeVM> _logger;
        private readonly Client _wjClient;

        public HomeVM(ILogger<HomeVM> logger, Client wjClient)
        {
            _logger = logger;
            _wjClient = wjClient;
            BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));
            VisitModlistWizardCommand = ReactiveCommand.Create(() =>
            {
                ProcessStartInfo processStartInfo = new(Consts.WabbajackModlistWizardUri.ToString())
                {
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);
            });
            this.WhenActivated(async disposables =>
            {
                try
                {
                    Modlists = await _wjClient.LoadLists().DisposeWith(disposables);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "While loading lists");
                }
            });
        }
        public ICommand VisitModlistWizardCommand { get; }
        public ICommand BrowseCommand { get; }
        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

        [Reactive]
        public ModlistMetadata[] Modlists { get; set; }
    }
}
