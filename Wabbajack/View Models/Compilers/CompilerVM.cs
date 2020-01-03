using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerVM : ViewModel
    {
        public MainWindowVM MWVM { get; }

        private readonly ObservableAsPropertyHelper<BitmapImage> _image;
        public BitmapImage Image => _image.Value;

        [Reactive]
        public ModManager SelectedCompilerType { get; set; }

        private readonly ObservableAsPropertyHelper<ISubCompilerVM> _compiler;
        public ISubCompilerVM Compiler => _compiler.Value;

        private readonly ObservableAsPropertyHelper<ModlistSettingsEditorVM> _currentModlistSettings;
        public ModlistSettingsEditorVM CurrentModlistSettings => _currentModlistSettings.Value;

        private readonly ObservableAsPropertyHelper<bool> _compiling;
        public bool Compiling => _compiling.Value;

        private readonly ObservableAsPropertyHelper<float> _percentCompleted;
        public float PercentCompleted => _percentCompleted.Value;

        public ObservableCollectionExtended<CPUDisplayVM> StatusList { get; } = new ObservableCollectionExtended<CPUDisplayVM>();

        public ObservableCollectionExtended<IStatusMessage> Log => MWVM.Log;

        public IReactiveCommand BackCommand { get; }
        public IReactiveCommand GoToModlistCommand { get; }
        public IReactiveCommand CloseWhenCompleteCommand { get; }
        public IReactiveCommand BeginCommand { get; }

        public FilePickerVM OutputLocation { get; }

        private readonly ObservableAsPropertyHelper<IUserIntervention> _ActiveGlobalUserIntervention;
        public IUserIntervention ActiveGlobalUserIntervention => _ActiveGlobalUserIntervention.Value;

        [Reactive]
        public bool StartedCompilation { get; set; }

        [Reactive]
        public ErrorResponse? Completed { get; set; }

        private readonly ObservableAsPropertyHelper<string> _progressTitle;
        public string ProgressTitle => _progressTitle.Value;

        public CompilerVM(MainWindowVM mainWindowVM)
        {
            MWVM = mainWindowVM;

            OutputLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select the folder to place the resulting modlist.wabbajack file",
            };

            // Load settings
            CompilerSettings settings = MWVM.Settings.Compiler;
            SelectedCompilerType = settings.LastCompiledModManager;
            OutputLocation.TargetPath = settings.OutputLocation;
            MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.LastCompiledModManager = SelectedCompilerType;
                    settings.OutputLocation = OutputLocation.TargetPath;
                })
                .DisposeWith(CompositeDisposable);

            // Swap to proper sub VM based on selected type
            _compiler = this.WhenAny(x => x.SelectedCompilerType)
                // Delay so the initial VM swap comes in immediately, subVM comes right after
                .DelayInitial(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Select<ModManager, ISubCompilerVM>(type =>
                {
                    switch (type)
                    {
                        case ModManager.MO2:
                            return new MO2CompilerVM(this);
                        case ModManager.Vortex:
                            return new VortexCompilerVM(this);
                        default:
                            return null;
                    }
                })
                // Unload old VM
                .Pairwise()
                .Do(pair =>
                {
                    pair.Previous?.Unload();
                })
                .Select(p => p.Current)
                .ToProperty(this, nameof(Compiler));

            // Let sub VM determine what settings we're displaying and when
            _currentModlistSettings = this.WhenAny(x => x.Compiler.ModlistSettings)
                .ToProperty(this, nameof(CurrentModlistSettings));

            _image = this.WhenAny(x => x.CurrentModlistSettings.ImagePath.TargetPath)
                // Throttle so that it only loads image after any sets of swaps have completed
                .Throttle(TimeSpan.FromMilliseconds(50), RxApp.TaskpoolScheduler)
                .DistinctUntilChanged()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(path =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return UIUtils.BitmapImageFromResource("Resources/Wabba_Mouth_No_Text.png");
                    if (UIUtils.TryGetBitmapImageFromFile(path, out var image))
                    {
                        return image;
                    }
                    return null;
                })
                .ToProperty(this, nameof(Image));

            _compiling = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .Select(compilation => compilation != null)
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(Compiling));

            BackCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    mainWindowVM.ActivePane = mainWindowVM.ModeSelectionVM;
                    StartedCompilation = false;
                    Completed = null;
                },
                canExecute: this.WhenAny(x => x.Compiling)
                    .Select(x => !x));

            // Compile progress updates and populate ObservableCollection
            Dictionary<int, CPUDisplayVM> cpuDisplays = new Dictionary<int, CPUDisplayVM>();
            this.WhenAny(x => x.Compiler.ActiveCompilation)
                .SelectMany(c => c?.QueueStatus ?? Observable.Empty<CPUStatus>())
                .ObserveOn(RxApp.TaskpoolScheduler)
                // Attach start times to incoming CPU items
                .Scan(
                    new CPUDisplayVM(),
                    (_, cpu) =>
                    {
                        var ret = cpuDisplays.TryCreate(cpu.ID);
                        ret.AbsorbStatus(cpu);
                        return ret;
                    })
                .ToObservableChangeSet(x => x.Status.ID)
                .Batch(TimeSpan.FromMilliseconds(50), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .Filter(i => i.Status.IsWorking && i.Status.ID != WorkQueue.UnassignedCpuId)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUDisplayVM>.Ascending(s => s.StartTime))
                .Bind(StatusList)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            _percentCompleted = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .StartWith(default(ACompiler))
                .CombineLatest(
                    this.WhenAny(x => x.Completed),
                    (compiler, completed) =>
                    {
                        if (compiler == null)
                        {
                            return Observable.Return<float>(completed != null ? 1f : 0f);
                        }
                        return compiler.PercentCompleted.StartWith(0);
                    })
                .Switch()
                .Debounce(TimeSpan.FromMilliseconds(25))
                .ToProperty(this, nameof(PercentCompleted));

            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: this.WhenAny(x => x.Compiler.CanCompile)
                    .Switch(),
                execute: async () =>
                {
                    try
                    {
                        await this.Compiler.Compile();
                        Completed = ErrorResponse.Success;
                    }
                    catch (Exception ex)
                    {
                        Completed = ErrorResponse.Fail(ex);
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Error(ex, $"Compiler error");
                    }
                });

            // When sub compiler begins a compile, mark state variable
            BeginCommand.StartingExecution()
                .Subscribe(_ =>
                {
                    StartedCompilation = true;
                })
                .DisposeWith(CompositeDisposable);

            // Listen for user interventions, and compile a dynamic list of all unhandled ones
            var activeInterventions = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .SelectMany(c => c?.LogMessages ?? Observable.Empty<IStatusMessage>())
                .WhereCastable<IStatusMessage, IUserIntervention>()
                .ToObservableChangeSet()
                .AutoRefresh(i => i.Handled)
                .Filter(i => !i.Handled)
                .AsObservableList();

            // Find the top intervention /w no CPU ID to be marked as "global"
            _ActiveGlobalUserIntervention = activeInterventions.Connect()
                .Filter(x => x.CpuID == WorkQueue.UnassignedCpuId)
                .QueryWhenChanged(query => query.FirstOrDefault())
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(ActiveGlobalUserIntervention));

            CloseWhenCompleteCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.Completed)
                    .Select(x => x != null),
                execute: () =>
                {
                    MWVM.ShutdownApplication();
                });

            GoToModlistCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.Completed)
                    .Select(x => x != null),
                execute: () =>
                {
                    if (string.IsNullOrWhiteSpace(OutputLocation.TargetPath))
                    {
                        Process.Start("explorer.exe", Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
                    }
                    else
                    {
                        Process.Start("explorer.exe", OutputLocation.TargetPath);
                    }
                });

            _progressTitle = Observable.CombineLatest(
                    this.WhenAny(x => x.Compiling),
                    this.WhenAny(x => x.StartedCompilation),
                    resultSelector: (compiling, started) =>
                    {
                        if (compiling)
                        {
                            return "Compiling";
                        }
                        else
                        {
                            return started ? "Compiled" : "Configuring";
                        }
                    })
                .ToProperty(this, nameof(ProgressTitle));
        }
    }
}
