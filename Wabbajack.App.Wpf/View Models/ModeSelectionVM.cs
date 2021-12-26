using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ModeSelectionVM : ViewModel
    {
        private MainWindowVM _mainVM;
        public ICommand BrowseCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CompileCommand { get; }
        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

        public ModeSelectionVM(MainWindowVM mainVM)
        {
            _mainVM = mainVM;

            InstallCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    var path = mainVM.Settings.Installer.LastInstalledListLocation;
                    if (path == default || !path.Exists)
                    {
                        path = UIUtils.OpenFileDialog($"*{Consts.ModListExtension}|*{Consts.ModListExtension}");
                    }
                    _mainVM.OpenInstaller(path);
                });

            CompileCommand = ReactiveCommand.Create(() => mainVM.NavigateTo(mainVM.Compiler.Value));
            BrowseCommand = ReactiveCommand.Create(() => mainVM.NavigateTo(mainVM.Gallery.Value));
        }
    }
}
