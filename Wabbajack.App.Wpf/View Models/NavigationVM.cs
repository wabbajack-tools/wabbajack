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
    public class NavigationVM : ViewModel
    {
        private readonly ILogger<NavigationVM> _logger;

        public NavigationVM(ILogger<NavigationVM> logger)
        {
            _logger = logger;
            InstallCommand = ReactiveCommand.Create(() =>
            {
                LoadLastLoadedModlist.Send();
                NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Installer);
            });
            CompileCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Compiler));
            BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModListGallery));
        }
        public ICommand BrowseCommand { get; set; }
        public ICommand InstallCommand { get; set; }
        public ICommand CompileCommand { get; set; }
        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
    }
}
