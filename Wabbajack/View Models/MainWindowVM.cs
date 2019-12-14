using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib;

namespace Wabbajack
{
    /// <summary>
    /// Main View Model for the application.
    /// Keeps track of which sub view is being shown in the window, and has some singleton wiring like WorkQueue and Logging.
    /// </summary>
    public class MainWindowVM : ViewModel
    {
        public MainWindow MainWindow { get; }

        public MainSettings Settings { get; }

        [Reactive]
        public ViewModel ActivePane { get; set; }

        public ObservableCollectionExtended<IStatusMessage> Log { get; } = new ObservableCollectionExtended<IStatusMessage>();

        public readonly Lazy<CompilerVM> Compiler;
        public readonly Lazy<InstallerVM> Installer;
        public readonly Lazy<ModListGalleryVM> Gallery;
        public readonly ModeSelectionVM ModeSelectionVM;
        public readonly WebBrowserVM WebBrowserVM;
        public readonly UserInterventionHandlers UserInterventionHandlers;
        public Dispatcher ViewDispatcher { get; set; }

        public ICommand CopyVersionCommand { get; }
        public string VersionDisplay { get; }

        public MainWindowVM(MainWindow mainWindow, MainSettings settings)
        {
            MainWindow = mainWindow;
            ViewDispatcher = MainWindow.Dispatcher;
            Settings = settings;
            Installer = new Lazy<InstallerVM>(() => new InstallerVM(this));
            Compiler = new Lazy<CompilerVM>(() => new CompilerVM(this));
            Gallery = new Lazy<ModListGalleryVM>(() => new ModListGalleryVM(this));
            ModeSelectionVM = new ModeSelectionVM(this);
            WebBrowserVM = new WebBrowserVM();
            UserInterventionHandlers = new UserInterventionHandlers(this);

            // Set up logging
            Utils.LogMessages
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet()
                .Buffer(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .Where(l => l.Count > 0)
                .ObserveOn(RxApp.MainThreadScheduler)
                .FlattenBufferResult()
                .Bind(Log)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            Utils.LogMessages
                .OfType<IUserIntervention>()
                .ObserveOnGuiThread()
                .SelectTask(msg => UserInterventionHandlers.Handle(msg))
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            if (IsStartingFromModlist(out var path))
            {
                Installer.Value.ModListLocation.TargetPath = path;
                ActivePane = Installer.Value;
            }
            else
            {
                // Start on mode selection
                ActivePane = ModeSelectionVM;
            }

            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                VersionDisplay = $"v{fvi.FileVersion}";
            }
            catch (Exception ex)
            {
                Utils.Error(ex);
                VersionDisplay = "ERROR";
            }
            CopyVersionCommand = ReactiveCommand.Create(() =>
            {
                Clipboard.SetText($"Wabbajack {VersionDisplay}\n{ThisAssembly.Git.Sha}");
            });
        }

        private static bool IsStartingFromModlist(out string modlistPath)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length != 3 || !args[1].Contains("-i"))
            {
                modlistPath = default;
                return false;
            }

            modlistPath = args[2];
            return true;
        }

        public void OpenInstaller(string path)
        {
            if (path == null) return;
            var installer = Installer.Value;
            Settings.Installer.LastInstalledListLocation = path;
            ActivePane = installer;
            installer.ModListLocation.TargetPath = path;
        }

        public void ShutdownApplication()
        {
            Dispose();
            Settings.PosX = MainWindow.Left;
            Settings.PosY = MainWindow.Top;
            Settings.Width = MainWindow.Width;
            Settings.Height = MainWindow.Height;
            MainSettings.SaveSettings(Settings);
            Application.Current.Shutdown();
        }
    }
}
