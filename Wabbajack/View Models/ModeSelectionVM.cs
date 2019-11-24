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

namespace Wabbajack
{
    public class ModeSelectionVM : ViewModel
    {
        public ObservableCollection<ModlistMetadata> ModLists { get; } = new ObservableCollection<ModlistMetadata>(ModlistMetadata.LoadFromGithub());

        [Reactive]
        public ModlistMetadata SelectedModList { get; set; }

        private MainWindowVM _mainVM;
        public ICommand DownloadAndInstallCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CompileCommand { get; }

        public ModeSelectionVM(MainWindowVM mainVM)
        {
            _mainVM = mainVM;
            InstallCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    OpenInstaller(
                        UIUtils.OpenFileDialog(
                            $"*{ExtensionManager.Extension}|*{ExtensionManager.Extension}",
                            initialDirectory: mainVM.Settings.Installer.LastInstalledListLocation));
                });

            CompileCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    mainVM.ActivePane = mainVM.Compiler.Value;
                });

            DownloadAndInstallCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.SelectedModList)
                    .Select(x => x != null)
                    .ObserveOnGuiThread(),
                execute: () =>
                {
                    OpenInstaller(Download());
                });
        }

        private void OpenInstaller(string path)
        {
            if (path == null) return;
            var installer = _mainVM.Installer.Value;
            _mainVM.Settings.Installer.LastInstalledListLocation = path;
            _mainVM.ActivePane = installer;
            installer.ModListPath = path;
        }

        private string Download()
        {
            if (!Directory.Exists(Consts.ModListDownloadFolder))
                Directory.CreateDirectory(Consts.ModListDownloadFolder);

            string dest = Path.Combine(Consts.ModListDownloadFolder, SelectedModList.Links.MachineURL + ExtensionManager.Extension);

            var window = new DownloadWindow(SelectedModList.Links.Download,
                                           SelectedModList.Title,
                                               SelectedModList.Links.DownloadMetadata?.Size ?? 0,
                                               dest);
            window.ShowDialog();

            if (window.Result == DownloadWindow.WindowResult.Completed)
                return dest;
            return null;
        }
    }
}
