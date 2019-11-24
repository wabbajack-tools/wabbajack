using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Wabbajack.Common;
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

        public ObservableCollectionExtended<string> Log { get; } = new ObservableCollectionExtended<string>();

        public readonly Lazy<CompilerVM> Compiler;
        public readonly Lazy<InstallerVM> Installer;
        public readonly ModeSelectionVM ModeSelectionVM;

        public MainWindowVM(MainWindow mainWindow, MainSettings settings)
        {
            MainWindow = mainWindow;
            Settings = settings;
            Installer = new Lazy<InstallerVM>(() => new InstallerVM(this));
            Compiler = new Lazy<CompilerVM>(() => new CompilerVM(this));
            ModeSelectionVM = new ModeSelectionVM(this);

            // Set up logging
            Utils.LogMessages
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet()
                .Buffer(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .Where(l => l.Count > 0)
                .ObserveOn(RxApp.MainThreadScheduler)
                .FlattenBufferResult()
                .Top(5000)
                .Bind(Log)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            // Start on mode selection
            ActivePane = ModeSelectionVM;
        }
    }
}
