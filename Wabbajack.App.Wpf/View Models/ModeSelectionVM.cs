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
        private MainWindowVM _mainVM;
        public ICommand BrowseCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CompileCommand { get; }
        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

        public ModeSelectionVM()
        {
            InstallCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    /* TODO 
                    var path = mainVM.Settings.Installer.LastInstalledListLocation;
                    if (path == default || !path.FileExists())
                    {
                        path = UIUtils.OpenFileDialog($"*{Ext.Wabbajack}|*{Ext.Wabbajack}");
                    }
                    _mainVM.OpenInstaller(path);
                    */
                });

            CompileCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Compiler));
            BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModListGallery));
        }
    }
}
