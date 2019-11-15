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
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;
using ReactiveUI.Fody.Helpers;
using System.Windows.Media;

namespace Wabbajack
{
    public class InstallerVM : ViewModel
    {
        public SlideShow Slideshow { get; }

        public MainWindowVM MWVM { get; }

        public BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Wabba_Mouth.png");

        private readonly ObservableAsPropertyHelper<ModListVM> _ModList;
        public ModListVM ModList => _ModList.Value;

        [Reactive]
        public string ModListPath { get; set; }

        [Reactive]
        public bool UIReady { get; set; }

        private readonly ObservableAsPropertyHelper<string> _HTMLReport;
        public string HTMLReport => _HTMLReport.Value;

        /// <summary>
        /// Tracks whether an install is currently in progress
        /// </summary>
        [Reactive]
        public bool Installing { get; set; }

        /// <summary>
        /// Tracks whether to show the installing pane
        /// </summary>
        [Reactive]
        public bool InstallingMode { get; set; }

        public FilePickerVM Location { get; }

        public FilePickerVM DownloadLocation { get; }

        private readonly ObservableAsPropertyHelper<float> _ProgressPercent;
        public float ProgressPercent => _ProgressPercent.Value;

        private readonly ObservableAsPropertyHelper<ImageSource> _Image;
        public ImageSource Image => _Image.Value;

        private readonly ObservableAsPropertyHelper<string> _TitleText;
        public string TitleText => _TitleText.Value;

        private readonly ObservableAsPropertyHelper<string> _AuthorText;
        public string AuthorText => _AuthorText.Value;

        private readonly ObservableAsPropertyHelper<string> _Description;
        public string Description => _Description.Value;

        private readonly ObservableAsPropertyHelper<string> _ProgressTitle;
        public string ProgressTitle => _ProgressTitle.Value;

        private readonly ObservableAsPropertyHelper<string> _ModListName;
        public string ModListName => _ModListName.Value;

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

            this.MWVM = mainWindowVM;
            this.ModListPath = source;

            this.Location = new FilePickerVM()
            {
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Installation Directory",
            };
            this.Location.AdditionalError = this.WhenAny(x => x.Location.TargetPath)
                .Select(x => Utils.IsDirectoryPathValid(x));
            this.DownloadLocation = new FilePickerVM()
            {
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select a location for MO2 downloads",
            };
            this.DownloadLocation.AdditionalError = this.WhenAny(x => x.DownloadLocation.TargetPath)
                .Select(x => Utils.IsDirectoryPathValid(x));

            // Load settings
            InstallationSettings settings = this.MWVM.Settings.InstallationSettings.TryCreate(source);
            this.Location.TargetPath = settings.InstallationLocation;
            this.DownloadLocation.TargetPath = settings.DownloadLocation;
            this.MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.InstallationLocation = this.Location.TargetPath;
                    settings.DownloadLocation = this.DownloadLocation.TargetPath;
                })
                .DisposeWith(this.CompositeDisposable);

            this._ModList = this.WhenAny(x => x.ModListPath)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(modListPath =>
                {
                    if (modListPath == null) return default(ModListVM);
                    var modList = Installer.LoadFromFile(modListPath);
                    if (modList == null)
                    {
                        MessageBox.Show("Invalid Modlist, or file not found.", "Invalid Modlist", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.MWVM.MainWindow.ExitWhenClosing = false;
                            var window = new ModeSelectionWindow
                            {
                                ShowActivated = true
                            };
                            window.Show();
                            this.MWVM.MainWindow.Close();
                        });
                        return default(ModListVM);
                    }
                    return new ModListVM(modList, modListPath);
                })
                .ObserveOnGuiThread()
                .StartWith(default(ModListVM))
                .ToProperty(this, nameof(this.ModList));
            this._HTMLReport = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ReportHTML)
                .ToProperty(this, nameof(this.HTMLReport));
            this._ProgressPercent = Observable.CombineLatest(
                    this.WhenAny(x => x.Installing),
                    this.WhenAny(x => x.InstallingMode),
                    resultSelector: (installing, mode) => !installing && mode)
                .Select(show => show ? 1f : 0f)
                // Disable for now, until more reliable
                //this.WhenAny(x => x.MWVM.QueueProgress)
                //    .Select(i => i / 100f)
                .ToProperty(this, nameof(this.ProgressPercent));

            this.Slideshow = new SlideShow(this);

            // Set display items to modlist if configuring or complete,
            // or to the current slideshow data if installing
            this._Image = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .SelectMany(x => x?.ImageObservable ?? Observable.Empty<BitmapImage>())
                        .NotNull()
                        .StartWith(WabbajackLogo),
                    this.WhenAny(x => x.Slideshow.Image)
                        .StartWith(default(BitmapImage)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, slideshow, installing) => installing ? slideshow : modList)
                .Select<BitmapImage, ImageSource>(x => x)
                .ToProperty(this, nameof(this.Image));
            this._TitleText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Name),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModName)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(this.TitleText));
            this._AuthorText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Author),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModAuthor)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(this.AuthorText));
            this._Description = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Description),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModDescription)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(this.Description));
            this._ModListName = this.WhenAny(x => x.ModList)
                .Select(x => x?.Name)
                .ToProperty(this, nameof(this.ModListName));

            // Define commands
            this.ShowReportCommand = ReactiveCommand.Create(ShowReport);
            this.OpenReadmeCommand = ReactiveCommand.Create(
                execute: this.OpenReadmeWindow,
                canExecute: this.WhenAny(x => x.ModList)
                    .Select(modList => !string.IsNullOrEmpty(modList?.Readme))
                    .ObserveOnGuiThread());
            this.BeginCommand = ReactiveCommand.Create(
                execute: this.ExecuteBegin,
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
            this.VisitWebsiteCommand = ReactiveCommand.Create(
                execute: () => Process.Start(this.ModList.Website),
                canExecute: this.WhenAny(x => x.ModList.Website)
                    .Select(x => x?.StartsWith("https://") ?? false)
                    .ObserveOnGuiThread());

            // Have Installation location updates modify the downloads location if empty
            this.WhenAny(x => x.Location.TargetPath)
                .Skip(1) // Don't do it initially
                .Subscribe(installPath =>
                {
                    if (string.IsNullOrWhiteSpace(this.DownloadLocation.TargetPath))
                    {
                        this.DownloadLocation.TargetPath = Path.Combine(installPath, "downloads");
                    }
                })
                .DisposeWith(this.CompositeDisposable);

            this._ProgressTitle = Observable.CombineLatest(
                    this.WhenAny(x => x.Installing),
                    this.WhenAny(x => x.InstallingMode),
                    resultSelector: (installing, mode) =>
                    {
                        if (!installing) return "Configuring";
                        return mode ? "Installing" : "Installed";
                    })
                .ToProperty(this, nameof(this.ProgressTitle));
        }

        private void ShowReport()
        {
            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, HTMLReport);
            Process.Start(file);
        }

        private void OpenReadmeWindow()
        {
            if (string.IsNullOrEmpty(this.ModList.Readme)) return;
            using (var fs = new FileStream(this.ModListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            using (var ms = new MemoryStream())
            {
                var entry = ar.GetEntry(this.ModList.Readme);
                if (entry == null)
                {
                    Utils.Log($"Tried to open a non-existant readme: {this.ModList.Readme}");
                    return;
                }
                using (var e = entry.Open())
                {
                    e.CopyTo(ms);
                }
                ms.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(ms))
                {
                    var viewer = new TextViewer(reader.ReadToEnd(), this.ModList.Name);
                    viewer.Show();
                }
            }
        }

        private void ExecuteBegin()
        {
            this.Installing = true;
            this.InstallingMode = true;
            var installer = new Installer(this.ModListPath, this.ModList.SourceModList, Location.TargetPath)
            {
                DownloadFolder = DownloadLocation.TargetPath
            };
            var th = new Thread(() =>
            {
                try
                {
                    installer.Install();
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

                    this.Installing = false;
                }
            })
            {
                Priority = ThreadPriority.BelowNormal
            };
            th.Start();
        }
    }
}