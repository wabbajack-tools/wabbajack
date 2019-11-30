using Alphaleonis.Win32.Filesystem;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.View_Models;

namespace Wabbajack
{
    public class ModeSelectionVM : ViewModel
    {
        public ObservableCollection<ModListMetadataVM> ModLists { get; }

        private MainWindowVM _mainVM;
        public ICommand DownloadAndInstallCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CompileCommand { get; }

        public ModeSelectionVM(MainWindowVM mainVM)
        {
            _mainVM = mainVM;

            ModLists = new ObservableCollection<ModListMetadataVM>(ModlistMetadata.LoadFromGithub().Select(m => new ModListMetadataVM(this, m)));

            InstallCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    var path = mainVM.Settings.Installer.LastInstalledListLocation;
                    if (string.IsNullOrWhiteSpace(path)
                        || !File.Exists(path))
                    {
                        path = UIUtils.OpenFileDialog($"*{ExtensionManager.Extension}|*{ExtensionManager.Extension}");
                    }
                    OpenInstaller(path);
                });

            CompileCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    mainVM.ActivePane = mainVM.Compiler.Value;
                });
        }

        internal void OpenInstaller(string path)
        {
            if (path == null) return;
            var installer = _mainVM.Installer.Value;
            _mainVM.Settings.Installer.LastInstalledListLocation = path;
            _mainVM.ActivePane = installer;
            installer.ModListPath.TargetPath = path;
        }
    }
}
