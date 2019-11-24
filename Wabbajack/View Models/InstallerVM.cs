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

namespace Wabbajack
{
    public class InstallerVM : ViewModel
    {
        public SlideShow Slideshow { get; }

        public MainWindowVM MWVM { get; }

        public BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Wabba_Mouth.png");

        private readonly ObservableAsPropertyHelper<ModListVM> _modList;
        public ModListVM ModList => _modList.Value;

        [Reactive]
        public string ModListPath { get; set; }

        [Reactive]
        public bool UIReady { get; set; }

        private readonly ObservableAsPropertyHelper<string> _htmlReport;
        public string HTMLReport => _htmlReport.Value;

        [Reactive]
        public AInstaller ActiveInstallation { get; private set; }

        private readonly ObservableAsPropertyHelper<bool> _installing;
        public bool Installing => _installing.Value;

        /// <summary>
        /// Tracks whether to show the installing pane
        /// </summary>
        [Reactive]
        public bool InstallingMode { get; set; }

        public FilePickerVM Location { get; }

        public FilePickerVM DownloadLocation { get; }

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
        public ObservableCollectionExtended<string> Log => MWVM.Log;

        // Command properties
        public IReactiveCommand BeginCommand { get; }
        public IReactiveCommand ShowReportCommand { get; }
        public IReactiveCommand OpenReadmeCommand { get; }
        public IReactiveCommand VisitWebsiteCommand { get; }

        public InstallerVM(MainWindowVM mainWindowVM, string source)
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
            ModListPath = source;

            Location = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.Off,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Installation Directory",
            };
            Location.AdditionalError = this.WhenAny(x => x.Location.TargetPath)
                .Select(x => Utils.IsDirectoryPathValid(x));
            DownloadLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.Off,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select a location for MO2 downloads",
            };
            DownloadLocation.AdditionalError = this.WhenAny(x => x.DownloadLocation.TargetPath)
                .Select(x => Utils.IsDirectoryPathValid(x));

            // Load settings
            ModlistInstallationSettings settings = MWVM.Settings.Installer.ModlistSettings.TryCreate(source);
            Location.TargetPath = settings.InstallationLocation;
            DownloadLocation.TargetPath = settings.DownloadLocation;
            MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.InstallationLocation = Location.TargetPath;
                    settings.DownloadLocation = DownloadLocation.TargetPath;
                })
                .DisposeWith(CompositeDisposable);

            _modList = this.WhenAny(x => x.ModListPath)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(modListPath =>
                {
                    if (modListPath == null) return default(ModListVM);
                    var modList = AInstaller.LoadFromFile(modListPath);
                    if (modList == null)
                    {
                        MessageBox.Show("Invalid Modlist, or file not found.", "Invalid Modlist", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MWVM.MainWindow.ExitWhenClosing = false;
                            var window = new ModeSelectionWindow
                            {
                                ShowActivated = true
                            };
                            window.Show();
                            MWVM.MainWindow.Close();
                        });
                        return default(ModListVM);
                    }
                    return new ModListVM(modList, modListPath);
                })
                .ObserveOnGuiThread()
                .StartWith(default(ModListVM))
                .ToProperty(this, nameof(ModList));
            _htmlReport = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ReportHTML)
                .ToProperty(this, nameof(HTMLReport));
            _installing = this.WhenAny(x => x.ActiveInstallation)
                .Select(compilation => compilation != null)
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(Installing));

            _percentCompleted = this.WhenAny(x => x.ActiveInstallation)
                .StartWith(default(AInstaller))
                .Pairwise()
                .Select(c =>
                {
                    if (c.Current == null)
                    {
                        return Observable.Return<float>(c.Previous == null ? 0f : 1f);
                    }
                    return c.Current.PercentCompleted;
                })
                .Switch()
                .Debounce(TimeSpan.FromMilliseconds(25))
                .ToProperty(this, nameof(PercentCompleted));

            Slideshow = new SlideShow(this);

            // Set display items to modlist if configuring or complete,
            // or to the current slideshow data if installing
            _image = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .SelectMany(x => x?.ImageObservable ?? Observable.Empty<BitmapImage>())
                        .NotNull()
                        .StartWith(WabbajackLogo),
                    this.WhenAny(x => x.Slideshow.Image)
                        .StartWith(default(BitmapImage)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, slideshow, installing) => installing ? slideshow : modList)
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
            _modListName = this.WhenAny(x => x.ModList)
                .Select(x => x?.Name)
                .ToProperty(this, nameof(ModListName));

            // Define commands
            ShowReportCommand = ReactiveCommand.Create(ShowReport);
            OpenReadmeCommand = ReactiveCommand.Create(
                execute: OpenReadmeWindow,
                canExecute: this.WhenAny(x => x.ModList)
                    .Select(modList => !string.IsNullOrEmpty(modList?.Readme))
                    .ObserveOnGuiThread());
            BeginCommand = ReactiveCommand.CreateFromTask(
                execute: ExecuteBegin,
                canExecute: Observable.CombineLatest(
                        this.WhenAny(x => x.Installing),
                        this.WhenAny(x => x.Location.InError),
                        this.WhenAny(x => x.DownloadLocation.InError),
                        resultSelector: (installing, loc, download) =>
                        {
                            if (installing) return false;
                            return !loc && !download;
                        })
                    .ObserveOnGuiThread());
            VisitWebsiteCommand = ReactiveCommand.Create(
                execute: () => Process.Start(ModList.Website),
                canExecute: this.WhenAny(x => x.ModList.Website)
                    .Select(x => x?.StartsWith("https://") ?? false)
                    .ObserveOnGuiThread());

            // Have Installation location updates modify the downloads location if empty
            this.WhenAny(x => x.Location.TargetPath)
                .Skip(1) // Don't do it initially
                .Subscribe(installPath =>
                {
                    if (string.IsNullOrWhiteSpace(DownloadLocation.TargetPath))
                    {
                        DownloadLocation.TargetPath = Path.Combine(installPath, "downloads");
                    }
                })
                .DisposeWith(CompositeDisposable);

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
            this.WhenAny(x => x.ActiveInstallation)
                .SelectMany(c => c?.QueueStatus ?? Observable.Empty<CPUStatus>())
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID), SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(StatusList)
                .Subscribe()
                .DisposeWith(CompositeDisposable);
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
            using (var fs = new FileStream(ModListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
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

        private async Task ExecuteBegin()
        {
            InstallingMode = true;
            AInstaller installer;
            
            try
            {
                installer = new MO2Installer(ModListPath, ModList.SourceModList, Location.TargetPath)
                {
                    DownloadFolder = DownloadLocation.TargetPath
                };
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                Utils.Log(ex.StackTrace);
                Utils.Log(ex.ToString());
                Utils.Log($"{ex.Message} - Can't continue");
                ActiveInstallation = null;
                return;
            }

            await Task.Run(async () =>
            {
                IDisposable subscription = null;
                try
                {
                    var workTask = installer.Begin();
                    ActiveInstallation = installer;
                    await workTask;
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null) ex = ex.InnerException;
                    Utils.Log(ex.StackTrace);
                    Utils.Log(ex.ToString());
                    Utils.Log($"{ex.Message} - Can't continue");
                }
                finally
                {
                    // Dispose of CPU tracking systems
                    subscription?.Dispose();
                    ActiveInstallation = null;
                }
            });
        }
    }
}
