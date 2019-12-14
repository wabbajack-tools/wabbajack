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

        /// <summary>
        /// Tracks whether installation has begun
        /// </summary>
        [Reactive]
        public bool InstallingMode { get; set; }

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

        public ObservableCollectionExtended<CPUStatus> StatusList { get; } = new ObservableCollectionExtended<CPUStatus>();
        public ObservableCollectionExtended<IStatusMessage> Log => MWVM.Log;

        private readonly ObservableAsPropertyHelper<ModManager?> _TargetManager;
        public ModManager? TargetManager => _TargetManager.Value;

        private readonly ObservableAsPropertyHelper<IUserIntervention> _ActiveGlobalUserIntervention;
        public IUserIntervention ActiveGlobalUserIntervention => _ActiveGlobalUserIntervention.Value;

        private readonly ObservableAsPropertyHelper<bool> _Completed;
        public bool Completed => _Completed.Value;

        // Command properties
        public IReactiveCommand ShowReportCommand { get; }
        public IReactiveCommand OpenReadmeCommand { get; }
        public IReactiveCommand VisitWebsiteCommand { get; }
        public IReactiveCommand BackCommand { get; }
        public IReactiveCommand CloseWhenCompleteCommand { get; }
        public IReactiveCommand GoToInstallCommand { get; }

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
                ExistCheckOption = FilePickerVM.ExistCheckOptions.On,
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
                .Select(compilation => compilation != null)
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
                    InstallingMode = false;
                    mainWindowVM.ActivePane = mainWindowVM.ModeSelectionVM;
                },
                canExecute: this.WhenAny(x => x.Installing)
                    .Select(x => !x));

            _Completed = Observable.CombineLatest(
                    this.WhenAny(x => x.Installing),
                    this.WhenAny(x => x.InstallingMode),
                resultSelector: (installing, installingMode) =>
                {
                    return installingMode && !installing;
                })
                .ToProperty(this, nameof(Completed));

            _percentCompleted = this.WhenAny(x => x.Installer.ActiveInstallation)
                .StartWith(default(AInstaller))
                .CombineLatest(
                    this.WhenAny(x => x.Completed),
                    (installer, completed) =>
                    {
                        if (installer == null)
                        {
                            return Observable.Return<float>(completed ? 1f : 0f);
                        }
                        return installer.PercentCompleted;
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
                        .SelectMany(x => x?.ImageObservable ?? Observable.Empty<BitmapImage>())
                        .NotNull()
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
                        return installing ? slideshow : modList;
                    })
                .Select<BitmapImage, ImageSource>(x => x)
                .ToProperty(this, nameof(Image));
            _titleText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Name),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModName)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(TitleText));
            _authorText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Author),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModAuthor)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(AuthorText));
            _description = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Description),
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
                execute: OpenReadmeWindow,
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
                    this.WhenAny(x => x.InstallingMode),
                    resultSelector: (installing, mode) =>
                    {
                        if (!installing) return "Configuring";
                        return mode ? "Installing" : "Installed";
                    })
                .ToProperty(this, nameof(ProgressTitle));

            // Compile progress updates and populate ObservableCollection
            this.WhenAny(x => x.Installer.ActiveInstallation)
                .SelectMany(c => c?.QueueStatus ?? Observable.Empty<CPUStatus>())
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .Filter(i => i.IsWorking)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID), SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(StatusList)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            // When sub installer begins an install, mark state variable
            this.WhenAny(x => x.Installer.BeginCommand)
                .Select(x => x?.StartingExecution() ?? Observable.Empty<Unit>())
                .Switch()
                .Subscribe(_ =>
                {
                    InstallingMode = true;
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
                canExecute: this.WhenAny(x => x.Completed),
                execute: () =>
                {
                    MWVM.ShutdownApplication();
                });

            GoToInstallCommand = ReactiveCommand.Create(
                canExecute: Observable.CombineLatest(
                    this.WhenAny(x => x.Completed),
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

        private void OpenReadmeWindow()
        {
            if (string.IsNullOrEmpty(ModList.Readme)) return;
            using (var fs = new FileStream(ModListLocation.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            using (var ms = new MemoryStream())
            {
                var entry = ar.GetEntry(ModList.Readme);
                if (entry == null)
                {
                    Utils.Log($"Tried to open a non-existant readme: {ModList.Readme}");
                    return;
                }
                using (var e = entry.Open())
                {
                    e.CopyTo(ms);
                }
                ms.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(ms))
                {
                    var viewer = new TextViewer(reader.ReadToEnd(), ModList.Name);
                    viewer.Show();
                }
            }
        }
    }
}
