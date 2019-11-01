using Syroot.Windows.IO;
using System;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using DynamicData;
using DynamicData.Binding;
using System.Reactive;
using System.Text;
using Wabbajack.Lib;
using Splat;

namespace Wabbajack
{
    public class InstallerVM : ViewModel
    {
        public SlideShow Slideshow { get; }

        public MainWindowVM MWVM { get; }

        public BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Banner_Dark.png");

        private readonly ObservableAsPropertyHelper<ModList> _ModList;
        public ModList ModList => _ModList.Value;

        private string _ModListPath;
        public string ModListPath { get => _ModListPath; set => this.RaiseAndSetIfChanged(ref _ModListPath, value); }

        private readonly ObservableAsPropertyHelper<string> _ModListName;
        public string ModListName => _ModListName.Value;

        private bool _UIReady;
        public bool UIReady { get => _UIReady; set => this.RaiseAndSetIfChanged(ref _UIReady, value); }

        private readonly ObservableAsPropertyHelper<string> _HTMLReport;
        public string HTMLReport => _HTMLReport.Value;

        /// <summary>
        /// Tracks whether an install is currently in progress
        /// </summary>
        private bool _Installing;
        public bool Installing { get => _Installing; set => this.RaiseAndSetIfChanged(ref _Installing, value); }

        /// <summary>
        /// Tracks whether to show the installing pane
        /// </summary>
        private bool _InstallingMode;
        public bool InstallingMode { get => _InstallingMode; set => this.RaiseAndSetIfChanged(ref _InstallingMode, value); }

        private string _Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public string Location { get => _Location; set => this.RaiseAndSetIfChanged(ref _Location, value); }

        private string _DownloadLocation;
        public string DownloadLocation { get => _DownloadLocation; set => this.RaiseAndSetIfChanged(ref _DownloadLocation, value); }

        private readonly ObservableAsPropertyHelper<float> _ProgressPercent;
        public float ProgressPercent => _ProgressPercent.Value;

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        // Command properties
        public IReactiveCommand BeginCommand { get; }
        public IReactiveCommand ShowReportCommand { get; }
        public IReactiveCommand OpenReadmeCommand { get; }

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

            this.MWVM = mainWindowVM;

            this._ModList = this.WhenAny(x => x.ModListPath)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(source =>
                {
                    if (source == null) return default;
                    var modlist = Installer.LoadFromFile(source);
                    if (modlist == null)
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
                        return default;
                    }
                    return modlist;
                })
                .ObserveOnGuiThread()
                .StartWith(default(ModList))
                .ToProperty(this, nameof(this.ModList));
            this._HTMLReport = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ReportHTML)
                .ToProperty(this, nameof(this.HTMLReport));
            this._ModListName = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.Name)
                .ToProperty(this, nameof(this.ModListName));
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

            // Locate and create modlist image if it exists
            var modListImage = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList),
                    this.WhenAny(x => x.ModListPath),
                    (modList, modListPath) => (modList, modListPath))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(u =>
                {
                    if (u.modList == null
                        || u.modListPath == null
                        || !File.Exists(u.modListPath)
                        || string.IsNullOrEmpty(u.modList.Image)
                        || u.modList.Image.Length != 36)
                    {
                        return WabbajackLogo;
                    }
                    try
                    {
                        using (var fs = new FileStream(u.modListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
                        using (var ms = new MemoryStream())
                        {
                            var entry = ar.GetEntry(u.modList.Image);
                            using (var e = entry.Open())
                                e.CopyTo(ms);
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.StreamSource = ms;
                            image.EndInit();
                            image.Freeze();

                            return image;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Log().Warn(ex, "Error loading modlist splash image.");
                        return WabbajackLogo;
                    }
                })
                .ObserveOnGuiThread()
                .StartWith(default(BitmapImage))
                .Replay(1)
                .RefCount();

            // Set displayed image to modlist image if configuring, or to the current slideshow image if installing
            this._Image = Observable.CombineLatest(
                modListImage
                    .StartWith(default(BitmapImage)),
                this.WhenAny(x => x.Slideshow.Image)
                    .StartWith(default(BitmapImage)),
                this.WhenAny(x => x.Installing)
                    .StartWith(false),
                resultSelector: (modList, slideshow, installing) =>
                {
                    return installing ? slideshow : modList;
                })
                .ToProperty(this, nameof(this.Image));

            // Define commands
            this.ShowReportCommand = ReactiveCommand.Create(ShowReport);
            this.OpenReadmeCommand = ReactiveCommand.Create(
                execute: this.OpenReadmeWindow,
                canExecute: this.WhenAny(x => x.ModList)
                    .Select(modList => !string.IsNullOrEmpty(modList?.Readme))
                    .ObserveOnGuiThread());
            this.BeginCommand = ReactiveCommand.Create(
                execute: this.ExecuteBegin,
                canExecute: this.WhenAny(x => x.Installing)
                    .Select(installing => !installing)
                    .ObserveOnGuiThread());

            // Have Installation location updates modify the downloads location if empty
            this.WhenAny(x => x.Location)
                .Subscribe(installPath =>
                {
                    if (string.IsNullOrWhiteSpace(this.DownloadLocation))
                    {
                       this.DownloadLocation = Path.Combine(installPath, "downloads");
                    }
                })
                .DisposeWith(this.CompositeDisposable);
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
                    var viewer = new TextViewer(reader.ReadToEnd(), this.ModListName);
                    viewer.Show();
                }
            }
        }

        private void ExecuteBegin()
        {
            this.Installing = true;
            this.InstallingMode = true;
            var installer = new Installer(this.ModListPath, this.ModList, Location)
            {
                DownloadFolder = DownloadLocation
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