using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

        private readonly ObservableAsPropertyHelper<ViewModel> _ActivePane;
        public ViewModel ActivePane => _ActivePane.Value;

        private int _QueueProgress;
        public int QueueProgress { get => _QueueProgress; set => this.RaiseAndSetIfChanged(ref _QueueProgress, value); }

        private readonly Subject<CPUStatus> _statusSubject = new Subject<CPUStatus>();
        public IObservable<CPUStatus> StatusObservable => _statusSubject;
        public ObservableCollectionExtended<CPUStatus> StatusList { get; } = new ObservableCollectionExtended<CPUStatus>();

        private Subject<string> _logSubj = new Subject<string>();
        public ObservableCollectionExtended<string> Log { get; } = new ObservableCollectionExtended<string>();

        private RunMode _Mode;
        public RunMode Mode { get => _Mode; set => this.RaiseAndSetIfChanged(ref _Mode, value); }

        private readonly Lazy<CompilerVM> _Compiler;
        private readonly Lazy<InstallerVM> _Installer;

        public MainWindowVM(RunMode mode, string source, MainWindow mainWindow)
        {
            this.Mode = mode;
            this.MainWindow = mainWindow;
            this._Installer = new Lazy<InstallerVM>(() => new InstallerVM(this));
            this._Compiler = new Lazy<CompilerVM>(() => new CompilerVM(this, source));

            // Set up logging
            _logSubj
                .ToObservableChangeSet()
                .Buffer(TimeSpan.FromMilliseconds(250))
                .Where(l => l.Count > 0)
                .FlattenBufferResult()
                .Top(5000)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(this.Log)
                .Subscribe()
                .DisposeWith(this.CompositeDisposable);
            Utils.SetLoggerFn(s => _logSubj.OnNext(s));
            Utils.SetStatusFn((msg, progress) => WorkQueue.Report(msg, progress));

            // Wire mode to drive the active pane.
            // Note:  This is currently made into a derivative property driven by mode,
            // but it can be easily changed into a normal property that can be set from anywhere if needed
            this._ActivePane = this.WhenAny(x => x.Mode)
                .Select<RunMode, ViewModel>(m =>
                {
                    switch (m)
                    {
                        case RunMode.Compile:
                            return this._Compiler.Value;
                        case RunMode.Install:
                            return this._Installer.Value;
                        default:
                            return default;
                    }
                })
                .ToProperty(this, nameof(this.ActivePane));
            this.WhenAny(x => x.ActivePane)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .WhereCastable<ViewModel, InstallerVM>()
                .Subscribe(vm => vm.ModListPath = source)
                .DisposeWith(this.CompositeDisposable);

            // Initialize work queue
            WorkQueue.Init(
                report_function: (id, msg, progress) => this._statusSubject.OnNext(new CPUStatus() { ID = id, Msg = msg, Progress = progress }),
                report_queue_size: (max, current) => this.SetQueueSize(max, current));

            // Compile progress updates and populate ObservableCollection
            this._statusSubject
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(250))
                .EnsureUniqueChanges()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID), SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(this.StatusList)
                .Subscribe()
                .DisposeWith(this.CompositeDisposable);
        }

        private void SetQueueSize(int max, int current)
        {
            if (max == 0)
                max = 1;
            QueueProgress = current * 100 / max;
        }

        public override void Dispose()
        {
            base.Dispose();
            Utils.SetLoggerFn(s => { });
        }
    }
}
