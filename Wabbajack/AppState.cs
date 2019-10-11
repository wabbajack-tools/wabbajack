using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wabbajack.Common;
using Wabbajack.NexusApi;
using Wabbajack.UI;

namespace Wabbajack
{
    public enum TaskMode { INSTALLING, BUILDING }
    internal class AppState : ViewModel, IDataErrorInfo
    {
        public const bool GcCollect = true;

        private SlideShow _slideShow;

        public bool installing = false;

        private string _mo2Folder;

        private ModList _modList;

        private readonly DateTime _startTime;

        public volatile bool Dirty;

        public readonly Dispatcher dispatcher;

        public AppState(Dispatcher d, TaskMode mode)
        {
            _wabbajackLogo = UIUtils.BitmapImageFromResource("Wabbajack.UI.banner.png");
            _splashScreenImage = _wabbajackLogo;
            _noneImage = UIUtils.BitmapImageFromResource("Wabbajack.UI.none.jpg");
            _nextIcon = UIUtils.BitmapImageFromResource("Wabbajack.UI.Icons.next.png");

            _slideShow = new SlideShow(this, true);

            if (Assembly.GetEntryAssembly().Location.ToLower().Contains("\\downloads\\"))
            {
                MessageBox.Show(
                    "This app seems to be running inside a folder called 'Downloads', such folders are often highly monitored by antivirus software and they can often " +
                    "conflict with the operations Wabbajack needs to perform. Please move this executable outside of your 'Downloads' folder and then restart the app.",
                    "Cannot run inside 'Downloads'",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }

            _startTime = DateTime.Now;

            Mode = mode;
            Dirty = false;
            dispatcher = d;

            slideshowThread = new Thread(UpdateLoop)
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            slideshowThread.Start();
        }

        private DateTime _lastSlideShowUpdate = new DateTime();

        public ObservableCollection<string> Log { get; } = new ObservableCollection<string>();
        public ObservableCollection<CPUStatus> Status { get; } = new ObservableCollection<CPUStatus>();

        private TaskMode _Mode;
        public TaskMode Mode { get => _Mode; set => this.RaiseAndSetIfChanged(ref _Mode, value); }

        private string _ModListName;
        public string ModListName { get => _ModListName; set => this.RaiseAndSetIfChanged(ref _ModListName, value); }

        private string _Location;
        public string Location { get => _Location; set => this.RaiseAndSetIfChanged(ref _Location, value); }

        private string _LocationLabel;
        public string LocationLabel { get => _LocationLabel; set => this.RaiseAndSetIfChanged(ref _LocationLabel, value); }

        private string _DownloadLocation;
        public string DownloadLocation { get => _DownloadLocation; set => this.RaiseAndSetIfChanged(ref _DownloadLocation, value); }

        public Visibility ShowReportButton => _htmlReport == null ? Visibility.Collapsed : Visibility.Visible;

        private string _htmlReport;
        public string HTMLReport
        {
            get => _htmlReport;
            set
            {
                _htmlReport = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ShowReportButton));
            }
        }

        private int _queueProgress;
        public int QueueProgress { get => _queueProgress; set => this.RaiseAndSetIfChanged(ref _queueProgress, value); }

        private List<CPUStatus> InternalStatus { get; } = new List<CPUStatus>();
        public string LogFile { get; }

        private ICommand _changePath;
        public ICommand ChangePath
        {
            get
            {
                if (_changePath == null) _changePath = new LambdaCommand(() => true, ExecuteChangePath);
                return _changePath;
            }
        }

        private ICommand _changeDownloadPath;
        public ICommand ChangeDownloadPath
        {
            get
            {
                if (_changeDownloadPath == null)
                    _changeDownloadPath = new LambdaCommand(() => true, ExecuteChangeDownloadPath);
                return _changeDownloadPath;
            }
        }

        private ICommand _begin;
        public ICommand Begin
        {
            get
            {
                if (_begin == null) _begin = new LambdaCommand(() => true, ExecuteBegin);
                return _begin;
            }
        }

        private ICommand _showReportCommand;
        public ICommand ShowReportCommand
        {
            get
            {
                return _showReportCommand ?? (_showReportCommand = new LambdaCommand(() => true, ShowReport));
            }
        }

        private ICommand _visitNexusSiteCommand;
        public ICommand VisitNexusSiteCommand
        {
            get
            {
                return _visitNexusSiteCommand ??
                       (_visitNexusSiteCommand = new LambdaCommand(() => true, VisitNexusSite));
            }
        }

        public ICommand OpenModListPropertiesCommand
        {
            get
            {
                return new LambdaCommand(() => true, OpenModListProperties);
            }
        }

        public ICommand SlideShowNextItem
        {
            get
            {
                return new LambdaCommand(() => true, _slideShow.UpdateSlideShowItem);
            }
        }

        private void ExecuteChangePath()
        {
            if (Mode == TaskMode.INSTALLING)
            {
                var folder = UIUtils.ShowFolderSelectionDialog("Select Installation directory");
                if (folder == null) return;
                Location = folder;
                if (DownloadLocation == null)
                    DownloadLocation = Path.Combine(Location, "downloads");
            }
            else
            {
                var folder = UIUtils.ShowFolderSelectionDialog("Select Your MO2 profile directory");
                Location = folder;
            }
        }

        private void ExecuteChangeDownloadPath()
        {
            var folder = UIUtils.ShowFolderSelectionDialog("Select a location for MO2 downloads");
            if (folder != null) DownloadLocation = folder;
        }

        private void ShowReport()
        {
            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, HTMLReport);
            Process.Start(file);
        }

        public string _nexusSiteURL = null;
        private void VisitNexusSite()
        {
            if (_nexusSiteURL != null && _nexusSiteURL.StartsWith("https://"))
            {
                Process.Start(_nexusSiteURL);
            }
        }

        private ModlistPropertiesWindow modlistPropertiesWindow;
        public string newImagePath;
        public string readmePath;
        public bool ChangedProperties;
        private void OpenModListProperties()
        {
            if (UIReady)
            {
                if (modlistPropertiesWindow == null)
                {
                    modlistPropertiesWindow = new ModlistPropertiesWindow(this);
                    newImagePath = null;
                    ChangedProperties = false;

                }
                if(!modlistPropertiesWindow.IsClosed)
                    modlistPropertiesWindow.Show();
                else
                {
                    modlistPropertiesWindow = null;
                    OpenModListProperties();
                }
            }
        }

        public bool HasReadme { get; set; }
        public ICommand OpenReadme
        {
            get
            {
                return new LambdaCommand(()=> true,OpenReadmeWindow);
            }
        }

        private void OpenReadmeWindow()
        {
            if (!UIReady || string.IsNullOrEmpty(_modList.Readme)) return;
            var text = "";
            using (var fs = new FileStream(_modListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            using (var ms = new MemoryStream())
            {
                var entry = ar.GetEntry(_modList.Readme);
                using (var e = entry.Open())
                    e.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using (var sr = new StreamReader(ms))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                        text += line+Environment.NewLine;
                    //text = sr.ReadToEnd();
                }
            }

            var viewer = new TextViewer(text, _ModListName);
            viewer.Show();
        }

        private bool _uiReady = false;
        public bool UIReady
        {
            get => _uiReady;
            set => this.RaiseAndSetIfChanged(ref _uiReady, value);
        }

        private readonly BitmapImage _wabbajackLogo = null;
        public readonly BitmapImage _noneImage = null;
        private BitmapImage _splashScreenImage = null;
        public BitmapImage SplashScreenImage
        {
            get => _splashScreenImage;
            set
            {
                _splashScreenImage = value;
                RaisePropertyChanged();
            }
        }

        private string _SplashScreenModName = "Wabbajack";
        public string SplashScreenModName { get => _SplashScreenModName; set => this.RaiseAndSetIfChanged(ref _SplashScreenModName, value); }
        private BitmapImage _nextIcon = null;
        public BitmapImage NextIcon { get => _nextIcon; set => this.RaiseAndSetIfChanged(ref _nextIcon, value); }

        private string _SplashScreenAuthorName = "Halgari & the Wabbajack Team";
        public string SplashScreenAuthorName { get => _SplashScreenAuthorName; set => this.RaiseAndSetIfChanged(ref _SplashScreenAuthorName, value); }

        private string _modListPath;

        private string _SplashScreenSummary;
        public string SplashScreenSummary { get => _SplashScreenSummary; set => this.RaiseAndSetIfChanged(ref _SplashScreenSummary, value); }
        private bool _splashShowNSFW = true;
        public bool SplashShowNSFW { get => _splashShowNSFW; set => this.RaiseAndSetIfChanged(ref _splashShowNSFW, value); }    
        private readonly Thread slideshowThread = null;
        private bool _enableSlideShow = true;
        public bool EnableSlideShow
        {
            get => _enableSlideShow;
            set
            {
                RaiseAndSetIfChanged(ref _enableSlideShow, value);
                if (!slideshowThread.IsAlive) return;
                if (!_enableSlideShow)
                {
                    ApplyModlistProperties();
                }
                else
                {
                    _slideShow.UpdateSlideShowItem();
                }
            }
        }

        public string Error => "Error";

        public string this[string columnName] => Validate(columnName);

        private string Validate(string columnName)
        {
            string validationMessage = null;
            switch (columnName)
            {
                case "Location":
                    if (Location == null)
                    {
                        validationMessage = null;
                    }
                    else switch (Mode)
                    {
                        case TaskMode.BUILDING when Location != null && Directory.Exists(Location) && File.Exists(Path.Combine(Location, "modlist.txt")):
                            Location = Path.Combine(Location, "modlist.txt");
                            validationMessage = null;
                            ConfigureForBuild();
                            break;
                        case TaskMode.INSTALLING when Location != null && Directory.Exists(Location) && !Directory.EnumerateFileSystemEntries(Location).Any():
                            validationMessage = null;
                            break;
                        case TaskMode.INSTALLING when Location != null && Directory.Exists(Location) && Directory.EnumerateFileSystemEntries(Location).Any():
                            validationMessage = "You have selected a non-empty directory. Installing the modlist here might result in a broken install!";
                            break;
                        default:
                            validationMessage = "Invalid Mod Organizer profile directory";
                            break;
                    }
                    break;
            }
            return validationMessage;
        }

        private void UpdateLoop()
        {
            while (Running)
            {
                if (Dirty)
                    lock (InternalStatus)
                    {
                        CPUStatus[] data = InternalStatus.ToArray();
                        dispatcher.Invoke(() =>
                        {
                            for (var idx = 0; idx < data.Length; idx += 1)
                                if (idx >= Status.Count)
                                    Status.Add(data[idx]);
                                else if (Status[idx] != data[idx])
                                    Status[idx] = data[idx];
                        });
                        Dirty = false;
                    }

                if (_slideShow.SlidesQueue.Any())
                {
                    if (DateTime.Now - _lastSlideShowUpdate > TimeSpan.FromSeconds(10))
                    {
                        _slideShow.UpdateSlideShowItem();
                    }
                }
                Thread.Sleep(10000);
            }
        }

        public bool Running { get; set; } = true;
        private void ApplyModlistProperties()
        {
            SplashScreenModName = _modList.Name;
            SplashScreenAuthorName = _modList.Author;
            _nexusSiteURL = _modList.Website;
            SplashScreenSummary = _modList.Description;
            if (!string.IsNullOrEmpty(_modList.Image) && _modList.Image.Length == 36)
            {
                SplashScreenImage = _wabbajackLogo;
                using (var fs = new FileStream(_modListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
                using (var ms = new MemoryStream())
                {
                    var entry = ar.GetEntry(_modList.Image);
                    using (var e = entry.Open())
                        e.CopyTo(ms);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();

                    SplashScreenImage = image;
                }
            }
            else
            {
                SplashScreenImage = _wabbajackLogo;
            }
        }

        public void LogMsg(string msg)
        {
            dispatcher.Invoke(() => Log.Add(msg));
        }

        public void SetProgress(int id, string msg, int progress)
        {
            lock (InternalStatus)
            {
                Dirty = true;
                while (id >= InternalStatus.Count) InternalStatus.Add(new CPUStatus());

                InternalStatus[id] = new CPUStatus { ID = id, Msg = msg, Progress = progress };
            }
        }

        public void SetQueueSize(int max, int current)
        {
            if (max == 0)
                max = 1;
            var total = current * 100 / max;
            QueueProgress = total;
        }

        private void ConfigureForBuild()
        {
            var profile_folder = Path.GetDirectoryName(Location);
            var mo2folder = Path.GetDirectoryName(Path.GetDirectoryName(profile_folder));
            if (!File.Exists(Path.Combine(mo2folder, "ModOrganizer.exe")))
                LogMsg($"Error! No ModOrganizer2.exe found in {mo2folder}");

            var profile_name = Path.GetFileName(profile_folder);
            ModListName = profile_name;
            Mode = TaskMode.BUILDING;

            var tmp_compiler = new Compiler(mo2folder);
            DownloadLocation = tmp_compiler.MO2DownloadsFolder;

            _mo2Folder = mo2folder;
        }

        internal void ConfigureForInstall(string source, ModList modlist)
        {

            _modList = modlist;
            _modListPath = source;
            Mode = TaskMode.INSTALLING;
            ModListName = _modList.Name;
            HTMLReport = _modList.ReportHTML;
            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ApplyModlistProperties();

            _slideShow.SlideShowElements = modlist.Archives.OfType<NexusMod>().Select(m => 
                new Slide(NexusApiUtils.FixupSummary(m.ModName),m.ModID,
                    NexusApiUtils.FixupSummary(m.Summary), NexusApiUtils.FixupSummary(m.Author),
                    m.Adult,m.NexusURL,m.SlideShowPic)).ToList();


            _slideShow.PreloadSlideShow();
        }

        private void ExecuteBegin()
        {
            UIReady = false;
            if (Mode == TaskMode.INSTALLING)
            {
                installing = true;
                var installer = new Installer(_modListPath, _modList, Location)
                {
                    DownloadFolder = DownloadLocation
                };
                var th = new Thread(() =>
                {
                    UIReady = false;
                    try
                    {
                        installer.Install();
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        LogMsg(ex.StackTrace);
                        LogMsg(ex.ToString());
                        LogMsg($"{ex.Message} - Can't continue");
                    }
                    finally
                    {
                        UIReady = true;
                        Running = false;
                        installing = false;
                        slideshowThread.Abort();
                    }
                })
                {
                    Priority = ThreadPriority.BelowNormal
                };
                th.Start();
            }
            else if (_mo2Folder != null)
            {
                var compiler = new Compiler(_mo2Folder)
                {
                    MO2Profile = ModListName,
                    ModListName = ChangedProperties ? SplashScreenModName : null,
                    ModListAuthor = ChangedProperties ? SplashScreenAuthorName : null,
                    ModListDescription = ChangedProperties ? SplashScreenSummary : null,
                    ModListImage = ChangedProperties ? newImagePath : null,
                    ModListWebsite = ChangedProperties ? _nexusSiteURL : null,
                    ModListReadme = ChangedProperties ? readmePath : null
                };
                var th = new Thread(() =>
                {
                    UIReady = false;
                    try
                    {
                        compiler.Compile();
                        if (compiler.ModList != null && compiler.ModList.ReportHTML != null)
                            HTMLReport = compiler.ModList.ReportHTML;
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        LogMsg(ex.StackTrace);
                        LogMsg(ex.ToString());
                        LogMsg($"{ex.Message} - Can't continue");
                    }
                    finally
                    {
                        UIReady = true;
                    }
                })
                {
                    Priority = ThreadPriority.BelowNormal
                };
                th.Start();
            }
            else
            {
                Utils.Log("Cannot compile modlist: no valid Mod Organizer profile directory selected.");
                UIReady = true;
            }
        }
    }

    public class CPUStatus
    {
        public int Progress { get; internal set; }
        public string Msg { get; internal set; }
        public int ID { get; internal set; }
    }
}