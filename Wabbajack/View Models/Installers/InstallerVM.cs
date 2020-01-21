using Syroot.Windows.IO;
using System;
using ReactiveUI;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;
using ReactiveUI.Fody.Helpers;
using System.Windows.Media;
using DynamicData;
using DynamicData.Binding;
using Wabbajack.Common.StatusFeed;
using System.Reactive;
using System.Collections.Generic;
using System.Windows.Input;

namespace Wabbajack
{
    public class InstallerVM : ViewModel, IBackNavigatingVM, ICpuStatusVM
    {
        public SlideShow Slideshow { get; }

        public MainWindowVM MWVM { get; }

        public static BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Wabbajack;component/Resources/Wabba_Mouth_No_Text.png")).Stream);
        public static BitmapImage WabbajackErrLogo { get; } = UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Wabbajack;component/Resources/Wabba_Ded.png")).Stream);

        private readonly ObservableAsPropertyHelper<ModListVM> _modList;
        public ModListVM ModList => _modList.Value;

        public FilePickerVM ModListLocation { get; }

        [Reactive]
        public ViewModel NavigateBackTarget { get; set; }

        private readonly ObservableAsPropertyHelper<ISubInstallerVM> _installer;
        public ISubInstallerVM Installer => _installer.Value;

        private readonly ObservableAsPropertyHelper<string> _htmlReport;
        public string HTMLReport => _htmlReport.Value;

        private readonly ObservableAsPropertyHelper<bool> _installing;
        public bool Installing => _installing.Value;

        [Reactive]
        public bool StartedInstallation { get; set; }

        [Reactive]
        public ErrorResponse? Completed { get; set; }

        private readonly ObservableAsPropertyHelper<ImageSource> _image;
        public ImageSource Image => _image.Value;

        private readonly ObservableAsPropertyHelper<string> _titleText;
        public string TitleText => _titleText.Value;

        private readonly ObservableAsPropertyHelper<string> _authorText;
        public string AuthorText => _authorText.Value;

        private readonly ObservableAsPropertyHelper<string> _description;
        public string Description => _description.Value;

        private readonly ObservableAsPropertyHelper<string> _progressTitle;
        public string ProgressTitle => _progressTitle.Value;

        private readonly ObservableAsPropertyHelper<string> _modListName;
        public string ModListName => _modListName.Value;

        private readonly ObservableAsPropertyHelper<float> _percentCompleted;
        public float PercentCompleted => _percentCompleted.Value;

        public ObservableCollectionExtended<CPUDisplayVM> StatusList { get; } = new ObservableCollectionExtended<CPUDisplayVM>();
        public ObservableCollectionExtended<IStatusMessage> Log => MWVM.Log;

        private readonly ObservableAsPropertyHelper<ModManager?> _TargetManager;
        public ModManager? TargetManager => _TargetManager.Value;

        private readonly ObservableAsPropertyHelper<IUserIntervention> _ActiveGlobalUserIntervention;
        public IUserIntervention ActiveGlobalUserIntervention => _ActiveGlobalUserIntervention.Value;

        private readonly ObservableAsPropertyHelper<(int CurrentCPUs, int DesiredCPUs)> _CurrentCpuCount;
        public (int CurrentCPUs, int DesiredCPUs) CurrentCpuCount => _CurrentCpuCount.Value;

        private readonly ObservableAsPropertyHelper<bool> _LoadingModlist;
        public bool LoadingModlist => _LoadingModlist.Value;

        private readonly ObservableAsPropertyHelper<bool> _IsActive;
        public bool IsActive => _IsActive.Value;

        // Command properties
        public ReactiveCommand<Unit, Unit> ShowManifestCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenReadmeCommand { get; }
        public ReactiveCommand<Unit, Unit> VisitModListWebsiteCommand { get; }
        public ReactiveCommand<Unit, Unit> BackCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseWhenCompleteCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToInstallCommand { get; }
        public ReactiveCommand<Unit, Unit> BeginCommand { get; }

        public InstallerVM(MainWindowVM mainWindowVM)
        {
            if (Path.GetDirectoryName(Assembly.GetEntryAssembly().Location.ToLower()) == KnownFolders.Downloads.Path.ToLower())
            {
                Utils.Log(new CriticalFailureIntervention(
                    "Wabbajack is running inside your Downloads folder. This folder is often highly monitored by antivirus software and these can often " +
                    "conflict with the operations Wabbajack needs to perform. Please move this executable outside of your Downloads folder and then restart the app.",
                    "Cannot run inside Downloads")).Task.Wait();
                Environment.Exit(1);
            }

            MWVM = mainWindowVM;

            ModListLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.File,
                PromptTitle = "Select a ModList to install"
            };

            // Swap to proper sub VM based on selected type
            _installer = this.WhenAny(x => x.TargetManager)
                // Delay so the initial VM swap comes in immediately, subVM comes right after
                .DelayInitial(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Select<ModManager?, ISubInstallerVM>(type =>
                {
                    switch (type)
                    {
                        case ModManager.MO2:
                            return new MO2InstallerVM(this);
                        case ModManager.Vortex:
                            return new VortexInstallerVM(this);
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
                .ToGuiProperty(this, nameof(Installer));

            // Load settings
            MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    MWVM.Settings.Installer.LastInstalledListLocation = ModListLocation.TargetPath;
                })
                .DisposeWith(CompositeDisposable);

            _IsActive = this.ConstructIsActive(MWVM)
                .ToGuiProperty(this, nameof(IsActive));

            // Active path represents the path to currently have loaded
            // If we're not actively showing, then "unload" the active path
            var activePath = Observable.CombineLatest(
                    this.WhenAny(x => x.ModListLocation.TargetPath),
                    this.WhenAny(x => x.IsActive),
                    resultSelector: (path, active) => (path, active))
                .Select(x =>
                {
                    if (!x.active) return default(string);
                    return x.path;
                })
                // Throttle slightly so changes happen more atomically
                .Throttle(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Replay(1)
                .RefCount();

            _modList = activePath
                .ObserveOn(RxApp.TaskpoolScheduler)
                // Convert from active path to modlist VM
                .Select(modListPath =>
                {
                    if (modListPath == null) return default(ModListVM);
                    if (!File.Exists(modListPath)) return default(ModListVM);
                    return new ModListVM(modListPath);
                })
                .DisposeOld()
                .ObserveOnGuiThread()
                .StartWith(default(ModListVM))
                .ToGuiProperty(this, nameof(ModList));

            // Force GC collect when modlist changes, just to make sure we clean up any loose large items immediately
            this.WhenAny(x => x.ModList)
                .Delay(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    GC.Collect();
                });

            _LoadingModlist = Observable.Merge(
                    // When active path changes, mark as loading
                    activePath
                        .Select(_ => true),
                    // When the resulting modlist comes in, mark it as done
                    this.WhenAny(x => x.ModList)
                        .Select(_ => false))
                .ToGuiProperty(this, nameof(LoadingModlist));
            _htmlReport = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ReportHTML)
                .ToGuiProperty(this, nameof(HTMLReport));
            _installing = this.WhenAny(x => x.Installer.ActiveInstallation)
                .Select(i => i != null)
                .ToGuiProperty(this, nameof(Installing));
            _TargetManager = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ModManager)
                .ToGuiProperty(this, nameof(TargetManager));

            // Add additional error check on ModList
            ModListLocation.AdditionalError = this.WhenAny(x => x.ModList)
                .Select<ModListVM, IErrorResponse>(modList =>
                {
                    if (modList == null) return ErrorResponse.Fail("ModList path resulted in a null object.");
                    if (modList.Error != null) return ErrorResponse.Fail("ModList is corrupt", modList.Error);
                    return ErrorResponse.Success;
                });

            BackCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    StartedInstallation = false;
                    Completed = null;
                    mainWindowVM.NavigateTo(mainWindowVM.ModeSelectionVM);
                },
                canExecute: Observable.CombineLatest(
                        this.WhenAny(x => x.Installing)
                            .Select(x => !x),
                        this.ConstructCanNavigateBack(),
                        resultSelector: (i, b) => i && b)
                    .ObserveOnGuiThread());

            _percentCompleted = this.WhenAny(x => x.Installer.ActiveInstallation)
                .StartWith(default(AInstaller))
                .CombineLatest(
                    this.WhenAny(x => x.Completed),
                    (installer, completed) =>
                    {
                        if (installer == null)
                        {
                            return Observable.Return<float>(completed != null ? 1f : 0f);
                        }
                        return installer.PercentCompleted.StartWith(0f);
                    })
                .Switch()
                .Debounce(TimeSpan.FromMilliseconds(25), RxApp.MainThreadScheduler)
                .ToGuiProperty(this, nameof(PercentCompleted));

            Slideshow = new SlideShow(this);

            // Set display items to ModList if configuring or complete,
            // or to the current slideshow data if installing
            _image = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Error),
                    this.WhenAny(x => x.ModList)
                        .Select(x => x?.ImageObservable ?? Observable.Return(default(BitmapImage)))
                        .Switch()
                        .StartWith(default(BitmapImage)),
                    this.WhenAny(x => x.Slideshow.Image)
                        .StartWith(default(BitmapImage)),
                    this.WhenAny(x => x.Installing),
                    this.WhenAny(x => x.LoadingModlist),
                    resultSelector: (err, modList, slideshow, installing, loading) =>
                    {
                        if (err != null)
                        {
                            return WabbajackErrLogo;
                        }
                        if (loading) return default;
                        return installing ? slideshow : modList;
                    })
                .Select<BitmapImage, ImageSource>(x => x)
                .ToGuiProperty(this, nameof(Image));
            _titleText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .Select(modList => modList?.Name ?? string.Empty),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModName)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToGuiProperty(this, nameof(TitleText));
            _authorText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .Select(modList => modList?.Author ?? string.Empty),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModAuthor)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToGuiProperty(this, nameof(AuthorText));
            _description = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .Select(modList => modList?.Description ?? string.Empty),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModDescription)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToGuiProperty(this, nameof(Description));
            _modListName = Observable.CombineLatest(
                        this.WhenAny(x => x.ModList.Error)
                            .Select(x => x != null),
                        this.WhenAny(x => x.ModList)
                            .Select(x => x?.Name),
                    resultSelector: (err, name) =>
                    {
                        if (err) return "Corrupted Modlist";
                        return name;
                    })
                .ToGuiProperty(this, nameof(ModListName));

            // Define commands
            ShowManifestCommand = ReactiveCommand.Create(ShowReport);
            OpenReadmeCommand = ReactiveCommand.Create(
                execute: () => this.ModList?.OpenReadmeWindow(),
                canExecute: this.WhenAny(x => x.ModList)
                    .Select(modList => !string.IsNullOrEmpty(modList?.Readme))
                    .ObserveOnGuiThread());
            VisitModListWebsiteCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    Process.Start(ModList.Website);
                    return Unit.Default;
                },
                canExecute: this.WhenAny(x => x.ModList.Website)
                    .Select(x => x?.StartsWith("https://") ?? false)
                    .ObserveOnGuiThread());

            _progressTitle = this.WhenAnyValue(
                    x => x.Installing,
                    x => x.StartedInstallation,
                    x => x.Completed,
                    selector: (installing, started, completed) =>
                    {
                        if (installing)
                        {
                            return "Installing";
                        }
                        else if (started)
                        {
                            if (completed == null) return "Installing";
                            return completed.Value.Succeeded ? "Installed" : "Failed";
                        }
                        else
                        {
                            return "Configuring";
                        }
                    })
                .ToGuiProperty(this, nameof(ProgressTitle));

            UIUtils.BindCpuStatus(
                this.WhenAny(x => x.Installer.ActiveInstallation)
                    .SelectMany(c => c?.QueueStatus ?? Observable.Empty<CPUStatus>()),
                StatusList)
                .DisposeWith(CompositeDisposable);

            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: this.WhenAny(x => x.Installer.CanInstall)
                    .Switch(),
                execute: async () =>
                {
                    try
                    {
                        var success = await this.Installer.Install();
                        Completed = ErrorResponse.Create(success);
                        try
                        {
                            this.ModList?.OpenReadmeWindow();
                        }
                        catch (Exception ex)
                        {
                            Utils.Error(ex);
                        }
                    }
                    catch (Exception ex)
                    { 
                        Utils.Error(ex, $"Encountered error, can't continue");
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Completed = ErrorResponse.Fail(ex);
                    }
                });

            // When sub installer begins an install, mark state variable
            BeginCommand.StartingExecution()
                .Subscribe(_ =>
                {
                    StartedInstallation = true;
                })
                .DisposeWith(CompositeDisposable);

            // Listen for user interventions, and compile a dynamic list of all unhandled ones
            var activeInterventions = this.WhenAny(x => x.Installer.ActiveInstallation)
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

            CloseWhenCompleteCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.Completed)
                    .Select(x => x != null),
                execute: () =>
                {
                    MWVM.ShutdownApplication();
                });

            GoToInstallCommand = ReactiveCommand.Create(
                canExecute: Observable.CombineLatest(
                    this.WhenAny(x => x.Completed)
                        .Select(x => x != null),
                    this.WhenAny(x => x.Installer.SupportsAfterInstallNavigation),
                    resultSelector: (complete, supports) => complete && supports),
                execute: () =>
                {
                    Installer.AfterInstallNavigation();
                });

            _CurrentCpuCount = this.WhenAny(x => x.Installer.ActiveInstallation.Queue.CurrentCpuCount)
                .Switch()
                .ToGuiProperty(this, nameof(CurrentCpuCount));
        }

        private void ShowReport()
        {
            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, HTMLReport);
            Process.Start(file);
        }
    }
}
