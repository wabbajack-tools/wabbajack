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

namespace Wabbajack
{
    public class ModeSelectionVM : ViewModel
    {
        public ICommand BrowseCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CompileCommand { get; }
        
        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

        public ModeSelectionVM()
        {
            InstallCommand = ReactiveCommand.Create(() =>
            {
                LoadLastLoadedModlist.Send();
                NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Installer);
            });
            CompileCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Compiler));
            BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModListGallery));
        }
    }
}
