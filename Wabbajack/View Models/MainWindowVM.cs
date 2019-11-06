using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
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

        public MainSettings Settings { get; }

        private readonly ObservableAsPropertyHelper<ViewModel> _ActivePane;
        public ViewModel ActivePane => _ActivePane.Value;

        private readonly ObservableAsPropertyHelper<int> _QueueProgress;
        public int QueueProgress => _QueueProgress.Value;

        public ObservableCollectionExtended<CPUStatus> StatusList { get; } = new ObservableCollectionExtended<CPUStatus>();

        public ObservableCollectionExtended<string> Log { get; } = new ObservableCollectionExtended<string>();

        [Reactive]
        public RunMode Mode { get; set; }

        private readonly Lazy<CompilerVM> _Compiler;
        private readonly Lazy<InstallerVM> _Installer;

        public MainWindowVM(RunMode mode, string source, MainWindow mainWindow, MainSettings settings)
        {
            this.Mode = mode;
            this.MainWindow = mainWindow;
            this.Settings = settings;
            this._Installer = new Lazy<InstallerVM>(() => new InstallerVM(this, source));
            this._Compiler = new Lazy<CompilerVM>(() => new CompilerVM(this, source));

            // Set up logging
            Utils.LogMessages
                .ToObservableChangeSet()
                .Buffer(TimeSpan.FromMilliseconds(250))
                .Where(l => l.Count > 0)
                .FlattenBufferResult()
                .Top(5000)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(this.Log)
                .Subscribe()
                .DisposeWith(this.CompositeDisposable);
            Utils.StatusUpdates
                .Subscribe((i) => WorkQueue.Report(i.Message, i.Progress))
                .DisposeWith(this.CompositeDisposable);

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

            // Compile progress updates and populate ObservableCollection
            WorkQueue.Status
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(250))
                .EnsureUniqueChanges()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID), SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(this.StatusList)
                .Subscribe()
                .DisposeWith(this.CompositeDisposable);

            this._QueueProgress = WorkQueue.QueueSize
                .Select(progress =>
                {
                    if (progress.Max == 0)
                    {
                        progress.Max = 1;
                    }
                    return progress.Current * 100 / progress.Max;
                })
                .ToProperty(this, nameof(this.QueueProgress));
        }
    }
}
