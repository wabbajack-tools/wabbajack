using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Wabbajack.Extensions;
using Wabbajack.Interventions;
using Wabbajack.Messages;
using Wabbajack.RateLimiter;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using ReactiveUI.Fody.Helpers;

namespace Wabbajack
{
    public class CompilerVM : BackNavigatingVM, IBackNavigatingVM, ICpuStatusVM
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

        private readonly ObservableAsPropertyHelper<Percent> _percentCompleted;
        public Percent PercentCompleted => _percentCompleted.Value;

        public ReadOnlyObservableCollection<CPUDisplayVM> StatusList { get; }

        public ObservableCollectionExtended<IStatusMessage> Log => MWVM.Log;

        public ReactiveCommand<Unit, Unit> GoToCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseWhenCompleteCommand { get; }
        public ReactiveCommand<Unit, Unit> BeginCommand { get; }

        public FilePickerVM OutputLocation { get; }

        private readonly ObservableAsPropertyHelper<IUserIntervention> _ActiveGlobalUserIntervention;
        public IUserIntervention ActiveGlobalUserIntervention => _ActiveGlobalUserIntervention.Value;

        [Reactive]
        public bool StartedCompilation { get; set; }

        [Reactive]
        public ErrorResponse? Completed { get; set; }

        private readonly ObservableAsPropertyHelper<string> _progressTitle;
        public string ProgressTitle => _progressTitle.Value;

        private readonly ObservableAsPropertyHelper<(int CurrentCPUs, int DesiredCPUs)> _CurrentCpuCount;
        public (int CurrentCPUs, int DesiredCPUs) CurrentCpuCount => _CurrentCpuCount.Value;
        
        public CompilerVM(ILogger<CompilerVM> logger, MainWindowVM mainWindowVM) : base(logger)
        {
            MWVM = mainWindowVM;
            
            OutputLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select the folder to export the compiled Wabbajack ModList to",
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
                        case ModManager.Standard:
                            return new MO2CompilerVM(this);
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
                .ToGuiProperty(this, nameof(Compiler));

            // Let sub VM determine what settings we're displaying and when
            _currentModlistSettings = this.WhenAny(x => x.Compiler.ModlistSettings)
                .ToGuiProperty(this, nameof(CurrentModlistSettings));

            _image = this.WhenAny(x => x.CurrentModlistSettings.ImagePath.TargetPath)
                // Throttle so that it only loads image after any sets of swaps have completed
                .Throttle(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .DistinctUntilChanged()
                .ObserveOnGuiThread()
                .Select(path =>
                {
                    if (path == default) return UIUtils.BitmapImageFromResource("Resources/Wabba_Mouth_No_Text.png");
                    return UIUtils.TryGetBitmapImageFromFile(path, out var image) ? image : null;
                })
                .ToGuiProperty(this, nameof(Image));

            _compiling = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .Select(compilation => compilation != null)
                .ToGuiProperty(this, nameof(Compiling));

            BackCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModeSelectionView);
                    StartedCompilation = false;
                    Completed = null;
                },
                canExecute: Observable.CombineLatest(
                        this.WhenAny(x => x.Compiling)
                            .Select(x => !x),
                        this.ConstructCanNavigateBack(),
                        resultSelector: (i, b) => i && b)
                    .ObserveOnGuiThread());

            /* TODO
            UIUtils.BindCpuStatus(
                this.WhenAny(x => x.Compiler.ActiveCompilation)
                    .SelectMany(c => c?.QueueStatus ?? Observable.Empty<CPUStatus>()),
                StatusList)
                .DisposeWith(CompositeDisposable);

            _percentCompleted = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .StartWith(default(ACompiler))
                .CombineLatest(
                    this.WhenAny(x => x.Completed),
                    (compiler, completed) =>
                    {
                        if (compiler == null)
                        {
                            return Observable.Return<Percent>(completed != null ? Percent.One : Percent.Zero);
                        }
                        return compiler.PercentCompleted.StartWith(Percent.Zero);
                    })
                .Switch()
                .Debounce(TimeSpan.FromMilliseconds(25), RxApp.MainThreadScheduler)
                .ToGuiProperty(this, nameof(PercentCompleted));

            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: this.WhenAny(x => x.Compiler.CanCompile)
                    .Switch(),
                execute: async () =>
                {
                    try
                    {
                        IsBackEnabledSubject.OnNext(false);
                        var modList = await this.Compiler.Compile();
                        Completed = ErrorResponse.Create(modList.Succeeded);
                    }
                    catch (Exception ex)
                    {
                        Completed = ErrorResponse.Fail(ex);
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Error(ex, $"Compiler error");
                    }
                    finally
                    {
                        IsBackEnabledSubject.OnNext(true);
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
                .ToGuiProperty(this, nameof(ActiveGlobalUserIntervention));

            CloseWhenCompleteCommand = ReactiveCommand.CreateFromTask(
                canExecute: this.WhenAny(x => x.Completed)
                    .Select(x => x != null),
                execute: async () =>
                {
                    await MWVM.ShutdownApplication();
                });

            GoToCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.Completed)
                    .Select(x => x != null),
                execute: () =>
                {
                    if (Completed?.Failed ?? false)
                    {
                        Process.Start("explorer.exe", $"/select,\"{Utils.LogFolder}\"");
                    }
                    else
                    {
                        Process.Start("explorer.exe",
                            OutputLocation.TargetPath == default
                                ? $"/select,\"{Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)}\""
                                : $"/select,\"{OutputLocation.TargetPath}\"");
                    }
                });

            _progressTitle = this.WhenAnyValue(
                    x => x.Compiling,
                    x => x.StartedCompilation,
                    x => x.Completed,
                    selector: (compiling, started, completed) =>
                    {
                        if (compiling)
                        {
                            return "Compiling";
                        }
                        else if (started)
                        {
                            if (completed == null) return "Compiling";
                            return completed.Value.Succeeded ? "Compiled" : "Failed";
                        }
                        else
                        {
                            return "Awaiting Input";
                        }
                    })
                .ToGuiProperty(this, nameof(ProgressTitle));

            _CurrentCpuCount = this.WhenAny(x => x.Compiler.ActiveCompilation.Queue.CurrentCpuCount)
                .Switch()
                .ToGuiProperty(this, nameof(CurrentCpuCount));
                                */

        }
    }
}
