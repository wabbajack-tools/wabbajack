using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.UI;

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

        public ObservableCollectionExtended<string> Log { get; } = new ObservableCollectionExtended<string>();

        public readonly Lazy<CompilerVM> Compiler;
        public readonly Lazy<InstallerVM> Installer;
        public readonly Lazy<ModListGalleryVM> Gallery;
        public readonly ModeSelectionVM ModeSelectionVM;

        public MainWindowVM(MainWindow mainWindow, MainSettings settings)
        {
            MainWindow = mainWindow;
            Settings = settings;
            Installer = new Lazy<InstallerVM>(() => new InstallerVM(this));
            Compiler = new Lazy<CompilerVM>(() => new CompilerVM(this));
            Gallery = new Lazy<ModListGalleryVM>(() => new ModListGalleryVM(this));
            ModeSelectionVM = new ModeSelectionVM(this);

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

            AModalWindowFactory.CurrentFactory = new MainWindowModalFactory(MainWindow.Dispatcher, this);

            if (IsStartingFromModlist(out var path))
            {
                Installer.Value.ModListPath.TargetPath = path;
                ActivePane = Installer.Value;
            }
            else
            {
                // Start on mode selection
                ActivePane = ModeSelectionVM;
            }
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
            installer.ModListPath.TargetPath = path;
        }
    }

    public class MainWindowModalFactory : AModalWindowFactory
    {
        private MainWindowVM _mainWindowVm;
        private ViewModel _prevVM;
        private TaskCompletionSource<object> _tcs;
        private Dispatcher _dispatcher;

        public MainWindowModalFactory(Dispatcher dispatcher, MainWindowVM mainWindowVm)
        {
            _mainWindowVm = mainWindowVm;
            _dispatcher = dispatcher;
        }

        public override Task<object> Show(InlinedWindowVM vm)
        {
            _tcs = new TaskCompletionSource<object>();
            _dispatcher.InvokeAsync(() =>
            {
                _prevVM = _mainWindowVm.ActivePane;
                _mainWindowVm.ActivePane = vm;
            });
            return _tcs.Task;
        }

        public override Task SetResult(object result)
        {
            return _dispatcher.InvokeAsync(() =>
            {
                _tcs.SetResult(result);
                _mainWindowVm.ActivePane = _prevVM;
            }).Task;
        }

        public override Task Cancel()
        {
            return _dispatcher.InvokeAsync(() =>
            {
                _tcs.SetCanceled();
                _mainWindowVm.ActivePane = _prevVM;
            }).Task;
        }
    }
}
