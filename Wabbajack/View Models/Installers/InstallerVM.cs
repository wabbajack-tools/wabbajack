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

namespace Wabbajack
{
    public class InstallerVM : ViewModel
    {
        public SlideShow Slideshow { get; }

        public MainWindowVM MWVM { get; }

        public BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Wabbajack;component/Resources/Wabba_Mouth_No_Text.png")).Stream);
        public BitmapImage WabbajackErrLogo { get; } = UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Wabbajack;component/Resources/Wabba_Ded.png")).Stream);

        private readonly ObservableAsPropertyHelper<ModListVM> _modList;
        public ModListVM ModList => _modList.Value;

        public FilePickerVM ModListLocation { get; }

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

        // Command properties
        public IReactiveCommand ShowReportCommand { get; }
        public IReactiveCommand OpenReadmeCommand { get; }
        public IReactiveCommand VisitWebsiteCommand { get; }
        public IReactiveCommand BackCommand { get; }
        public IReactiveCommand CloseWhenCompleteCommand { get; }
        public IReactiveCommand GoToInstallCommand { get; }
        public IReactiveCommand BeginCommand { get; }

        public InstallerVM(MainWindowVM mainWindowVM)
        {
            if (Path.GetDirectoryName(Assembly.GetEntryAssembly().Location.ToLower()) == KnownFolders.Downloads.Path.ToLower())
            {
                MessageBox.Show(
                    "Wabbajack is running inside your Downloads folder. This folder is often highly monitored by antivirus software and these can often " +
                    "conflict with the operations Wabbajack needs to perform. Please move this executable outside of your Downloads folder and then restart the app.",
                    "Cannot run inside Downloads",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }

            MWVM = mainWindowVM;

            ModListLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.File,
                PromptTitle = "Select a modlist to install"
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
                .ToProperty(this, nameof(Installer));

            // Load settings
            MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    MWVM.Settings.Installer.LastInstalledListLocation = ModListLocation.TargetPath;
                })
                .DisposeWith(CompositeDisposable);

            _modList = this.WhenAny(x => x.ModListLocation.TargetPath)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(modListPath =>
                {
                    if (modListPath == null) return default(ModListVM);
                    if (!File.Exists(modListPath)) return default(ModListVM);
                    return new ModListVM(modListPath);
                })
                .ObserveOnGuiThread()
                .StartWith(default(ModListVM))
                .ToProperty(this, nameof(ModList));
            _htmlReport = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ReportHTML)
                .ToProperty(this, nameof(HTMLReport));
            _installing = this.WhenAny(x => x.Installer.ActiveInstallation)
                .Select(i => i != null)
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(Installing));
            _TargetManager = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ModManager)
                .ToProperty(this, nameof(TargetManager));

            // Add additional error check on modlist
            ModListLocation.AdditionalError = this.WhenAny(x => x.ModList)
                .Select<ModListVM, IErrorResponse>(modList =>
                {
                    if (modList == null) return ErrorResponse.Fail("Modlist path resulted in a null object.");
                    if (modList.Error != null) return ErrorResponse.Fail("Modlist is corrupt", modList.Error);
                    return ErrorResponse.Success;
                });

            BackCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    StartedInstallation = false;
                    Completed = null;
                    mainWindowVM.ActivePane = mainWindowVM.ModeSelectionVM;
                },
                canExecute: this.WhenAny(x => x.Installing)
                    .Select(x => !x));

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
                .Debounce(TimeSpan.FromMilliseconds(25))
                .ToProperty(this, nameof(PercentCompleted));

            Slideshow = new SlideShow(this);

            // Set display items to modlist if configuring or complete,
            // or to the current slideshow data if installing
            _image = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Error),
                    this.WhenAny(x => x.ModList)
                        .Select(x => x?.ImageObservable ?? Observable.Return(WabbajackLogo))
                        .Switch()
                        .StartWith(WabbajackLogo),
                    this.WhenAny(x => x.Slideshow.Image)
                        .StartWith(default(BitmapImage)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (err, modList, slideshow, installing) =>
                    {
                        if (err != null)
                        {
                            return WabbajackErrLogo;
                        }
                        var ret = installing ? slideshow : modList;
                        return ret ?? WabbajackLogo;
                    })
                .Select<BitmapImage, ImageSource>(x => x)
                .ToProperty(this, nameof(Image));
            _titleText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .Select(modList => modList?.Name ?? string.Empty),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModName)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(TitleText));
            _authorText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .Select(modList => modList?.Author ?? string.Empty),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModAuthor)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(AuthorText));
            _description = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .Select(modList => modList?.Description ?? string.Empty),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModDescription)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(Description));
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
                .ToProperty(this, nameof(ModListName));

            // Define commands
            ShowReportCommand = ReactiveCommand.Create(ShowReport);
            OpenReadmeCommand = ReactiveCommand.Create(
                execute: () => this.ModList?.OpenReadmeWindow(),
                canExecute: this.WhenAny(x => x.ModList)
                    .Select(modList => !string.IsNullOrEmpty(modList?.Readme))
                    .ObserveOnGuiThread());
            VisitWebsiteCommand = ReactiveCommand.Create(
                execute: () => Process.Start(ModList.Website),
                canExecute: this.WhenAny(x => x.ModList.Website)
                    .Select(x => x?.StartsWith("https://") ?? false)
                    .ObserveOnGuiThread());

            _progressTitle = Observable.CombineLatest(
                    this.WhenAny(x => x.Installing),
                    this.WhenAny(x => x.StartedInstallation),
                    resultSelector: (installing, started) =>
                    {
                        if (!installing) return "Configuring";
                        return started ? "Installing" : "Installed";
                    })
                .ToProperty(this, nameof(ProgressTitle));

            Dictionary<int, CPUDisplayVM> cpuDisplays = new Dictionary<int, CPUDisplayVM>();
            // Compile progress updates and populate ObservableCollection
            this.WhenAny(x => x.Installer.ActiveInstallation)
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
                .Batch(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .Filter(i => i.Status.IsWorking && i.Status.ID != WorkQueue.UnassignedCpuId)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUDisplayVM>.Ascending(s => s.StartTime))
                .Bind(StatusList)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: this.WhenAny(x => x.Installer.CanInstall)
                    .Switch(),
                execute: async () =>
                {
                    try
                    {
                        await this.Installer.Install();
                        Completed = ErrorResponse.Success;
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
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Log(ex.StackTrace);
                        Utils.Log(ex.ToString());
                        Utils.Log($"{ex.Message} - Can't continue");
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
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(ActiveGlobalUserIntervention));

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
        }

        private void ShowReport()
        {
            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, HTMLReport);
            Process.Start(file);
        }
    }
}
