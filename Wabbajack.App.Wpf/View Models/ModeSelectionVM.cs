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
    public class ModeSelectionVM : ViewModel
    {
        private readonly ILogger<ModeSelectionVM> _logger;
        private readonly Client _wjClient;

        public ModeSelectionVM(Client wjClient)
        {
            _wjClient = wjClient;
            InstallCommand = ReactiveCommand.Create(() =>
            {
                LoadLastLoadedModlist.Send();
                NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Installer);
            });
            CompileCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Compiler));
            BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModListGallery));
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
        public ICommand BrowseCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CompileCommand { get; }
        public ICommand VisitModlistWizardCommand { get; }
        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

        [Reactive]
        public ModlistMetadata[] Modlists { get; set; }
    }
}
