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

        private readonly ObservableAsPropertyHelper<ViewModel> _ActivePane;
        public ViewModel ActivePane => _ActivePane.Value;

        public ObservableCollectionExtended<CPUStatus> StatusList { get; } = new ObservableCollectionExtended<CPUStatus>();

        public ObservableCollectionExtended<string> Log { get; } = new ObservableCollectionExtended<string>();

        [Reactive]
        public RunMode Mode { get; set; }

        private readonly Lazy<CompilerVM> _Compiler;
        private readonly Lazy<InstallerVM> _Installer;

        public MainWindowVM(RunMode mode, string source, MainWindow mainWindow, MainSettings settings)
        {
            Mode = mode;
            MainWindow = mainWindow;
            Settings = settings;
            _Installer = new Lazy<InstallerVM>(() => new InstallerVM(this, source));
            _Compiler = new Lazy<CompilerVM>(() => new CompilerVM(this));

            // Set up logging
            Utils.LogMessages
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet()
                .Buffer(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .Where(l => l.Count > 0)
                .FlattenBufferResult()
                .Top(5000)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(Log)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            // Wire mode to drive the active pane.
            // Note:  This is currently made into a derivative property driven by mode,
            // but it can be easily changed into a normal property that can be set from anywhere if needed
            _ActivePane = this.WhenAny(x => x.Mode)
                .Select<RunMode, ViewModel>(m =>
                {
                    switch (m)
                    {
                        case RunMode.Compile:
                            return _Compiler.Value;
                        case RunMode.Install:
                            return _Installer.Value;
                        default:
                            return default;
                    }
                })
                .ToProperty(this, nameof(ActivePane));


            // Compile progress updates and populate ObservableCollection
            /*
            _Compiler.WhenAny(c => c.Value.Compiler.)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.)
                /*
                .Batch(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID), SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(this.StatusList)
                .Subscribe()
                .DisposeWith(this.CompositeDisposable);*/
        }
    }
}
