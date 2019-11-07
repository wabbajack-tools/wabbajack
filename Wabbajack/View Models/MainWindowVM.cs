using System;
using System.IO;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Syroot.Windows.IO;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public enum Page
    {
        StartUp, Gallery, InstallerConfig, CompilerConfig, Installer, Compiler
    }

    public class MainWindowVM : ViewModel
    {
        public MainWindow MainWindow { get; }

        public MainSettings Settings { get; }

        private readonly ObservableAsPropertyHelper<ViewModel> _ContentArea;
        public ViewModel ContentArea => _ContentArea.Value;

        [Reactive]
        public Page CurrentPage { get; set; } = Page.StartUp;

        private readonly Lazy<StartupVM> _startupScreen;
        private readonly Lazy<ModListGalleryVM> _modlistGallery;
        private readonly Lazy<InstallationConfigVM> _installerConfig;
        private readonly Lazy<InstallerVM> _installer;

        public MainWindowVM(MainWindow mainWindow, MainSettings settings)
        {
            MainWindow = mainWindow;
            Settings = settings;
            _startupScreen = new Lazy<StartupVM>(()=>new StartupVM(this));
            _modlistGallery = new Lazy<ModListGalleryVM>(()=> new ModListGalleryVM(this));
            _installerConfig = new Lazy<InstallationConfigVM>(() => new InstallationConfigVM(this));
            _installer = new Lazy<InstallerVM>(() => new InstallerVM(this));

            if (Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location.ToLower()) == KnownFolders.Downloads.Path.ToLower())
            {
                MessageBox.Show(
                    "Wabbajack is running inside your Downloads folder. This folder is often highly monitored by antivirus software and these can often " +
                    "conflict with the operations Wabbajack needs to perform. Please move this executable outside of your Downloads folder and then restart the app.",
                    "Cannot run inside Downloads",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }

            _ContentArea = this.WhenAny(x => x.CurrentPage)
                .Select<Page, ViewModel>(m =>
                {
                    switch (m)
                    {
                        case Page.StartUp: return _startupScreen.Value;
                        case Page.Gallery: return _modlistGallery.Value;
                        case Page.InstallerConfig: return _installerConfig.Value;
                        case Page.CompilerConfig: return default;
                        case Page.Installer: return _installer.Value;
                        case Page.Compiler: return default;
                        default: return default;
                    }
                })
                .ToProperty(this, nameof(ContentArea));
        }

        public void Install(ModListVM modList, string installPath, string downloadPath)
        {
            _installer.Value.ModList = modList;
            _installer.Value.InstallPath = installPath;
            _installer.Value.DownloadPath = downloadPath;
            CurrentPage = Page.Installer;
        }
    }
}
