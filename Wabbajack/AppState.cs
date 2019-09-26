using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wabbajack.Common;
using Wabbajack.NexusApi;

namespace Wabbajack
{
    internal class AppState : INotifyPropertyChanged, IDataErrorInfo
    {
        private ICommand _begin;

        private ICommand _changeDownloadPath;

        private ICommand _changePath;
        private string _downloadLocation;

        private string _htmlReport;

        private bool _ignoreMissingFiles;
        private string _location;

        private string _mo2Folder;


        private string _mode;
        private ModList _modList;
        private string _modListName;

        private int _queueProgress;

        private ICommand _showReportCommand;
        private ICommand _visitNexusSiteCommand;

        private readonly DateTime _startTime;

        public volatile bool Dirty;

        private readonly Dispatcher dispatcher;

        public AppState(Dispatcher d, string mode)
        {

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.banner.png");
            image.EndInit();
            _wabbajackLogo = image;
            _splashScreenImage = image;

            SetupSlideshow();

            if (Assembly.GetEntryAssembly().Location.ToLower().Contains("\\downloads\\"))
            {
                MessageBox.Show(
                    "This app seems to be running inside a folder called `Downloads`, such folders are often highly monitored by antivirus software and they can often " +
                    "conflict with the operations Wabbajack needs to perform. Please move this executable outside of your `Downloads` folder and then restart the app.",
                    "Cannot run inside `Downloads`",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }

            _startTime = DateTime.Now;
            LogFile = Assembly.GetExecutingAssembly().Location + ".log";

            if (LogFile.FileExists())
                File.Delete(LogFile);

            Mode = mode;
            Dirty = false;
            dispatcher = d;
            Log = new ObservableCollection<string>();
            Status = new ObservableCollection<CPUStatus>();
            InternalStatus = new List<CPUStatus>();

            var th = new Thread(() => UpdateLoop());
            th.Priority = ThreadPriority.BelowNormal;
            th.IsBackground = true;
            th.Start();
        }

        private void SetupSlideshow()
        {
            var files = NexusApiClient.CachedSlideShow;
            if (files.Any())
            {
                SlideShowElements = files.ToList();
            }
        }

        public Random _random = new Random();
        public List<SlideShowItem> SlideShowElements = new List<SlideShowItem>();
        private DateTime _lastSlideShowUpdate = new DateTime();

        public ObservableCollection<string> Log { get; }
        public ObservableCollection<CPUStatus> Status { get; }

        public string Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                OnPropertyChanged("Mode");
            }
        }

        public string ModListName
        {
            get => _modListName;
            set
            {
                _modListName = value;
                OnPropertyChanged("ModListName");
            }
        }

        public string Location
        {
            get => _location;
            set
            {
                _location = value;
                OnPropertyChanged("Location");
            }
        }
        

        public string DownloadLocation
        {
            get => _downloadLocation;
            set
            {
                _downloadLocation = value;
                OnPropertyChanged("DownloadLocation");
            }
        }

        public Visibility ShowReportButton => _htmlReport == null ? Visibility.Collapsed : Visibility.Visible;

        public string HTMLReport
        {
            get => _htmlReport;
            set
            {
                _htmlReport = value;
                OnPropertyChanged("HTMLReport");
                OnPropertyChanged("ShowReportButton");
            }
        }

        public int QueueProgress
        {
            get => _queueProgress;
            set
            {
                if (value != _queueProgress)
                {
                    _queueProgress = value;
                    OnPropertyChanged("QueueProgress");
                }
            }
        }


        private List<CPUStatus> InternalStatus { get; }
        public string LogFile { get; }

        public ICommand ChangePath
        {
            get
            {
                if (_changePath == null) _changePath = new LambdaCommand(() => true, () => ExecuteChangePath());
                return _changePath;
            }
        }

        public ICommand ChangeDownloadPath
        {
            get
            {
                if (_changeDownloadPath == null)
                    _changeDownloadPath = new LambdaCommand(() => true, () => ExecuteChangeDownloadPath());
                return _changeDownloadPath;
            }
        }

        public ICommand Begin
        {
            get
            {
                if (_begin == null) _begin = new LambdaCommand(() => true, () => ExecuteBegin());
                return _begin;
            }
        }

        public ICommand ShowReportCommand
        {
            get
            {
                if (_showReportCommand == null) _showReportCommand = new LambdaCommand(() => true, () => ShowReport());
                return _showReportCommand;
            }
        }

        public ICommand VisitNexusSiteCommand
        {
            get
            {
                if (_visitNexusSiteCommand == null) _visitNexusSiteCommand = new LambdaCommand(() => true, () => VisitNexusSite());
                return _visitNexusSiteCommand;
            }
        }

        public string _nexusSiteURL = null;

        private void VisitNexusSite()
        {
            if (_nexusSiteURL != null && _nexusSiteURL.StartsWith("https://"))
            {
                Process.Start(_nexusSiteURL);
            }
        }


        private bool _uiReady = false;
        public bool UIReady
        {
            get => _uiReady;
            set
            {
                _uiReady = value;
                OnPropertyChanged("UIReady");
            }
        }

        private BitmapImage _wabbajackLogo = null;
        private BitmapImage _splashScreenImage = null;
        public BitmapImage SplashScreenImage
        {
            get => _splashScreenImage;
            set
            {
                _splashScreenImage = value;
                OnPropertyChanged("SplashScreenImage");
            }
        }

        public string _splashScreenModName = "Wabbajack";
        public string SplashScreenModName
        {
            get => _splashScreenModName;
            set
            {
                _splashScreenModName = value;
                OnPropertyChanged("SplashScreenModName");
            }
        }

        public string _splashScreenAuthorName = "Halgari & the Wabbajack Team";
        public string SplashScreenAuthorName
        {
            get => _splashScreenAuthorName;
            set
            {
                _splashScreenAuthorName = value;
                OnPropertyChanged("SplashScreenAuthorName");
            }
        }

        public string _splashScreenSummary = "";
        public string SplashScreenSummary
        {
            get => _splashScreenSummary;
            set
            {
                _splashScreenSummary = value;
                OnPropertyChanged("SplashScreenSummary");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string name)
        {
            if(PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
        public string Error
        {
            get { return "Error"; }
        }

        public string this[string columnName]
        {
            get
            {
                return Validate(columnName);
            }
        }
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
                    else if (Location != null && Directory.Exists(Location) && File.Exists(Path.Combine(Location, "modlist.txt")))
                    {
                        Location = Path.Combine(Location, "modlist.txt");
                        validationMessage = null;
                        ConfigureForBuild();
                    }
                    else
                    {
                        validationMessage = "Invalid Mod Organizer profile directory";
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
                        var data = InternalStatus.ToArray();
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

                if (SlideShowElements.Any())
                {
                    if (DateTime.Now - _lastSlideShowUpdate > TimeSpan.FromSeconds(10))
                    {
                        dispatcher.Invoke(() =>
                        {
                            var element = SlideShowElements[_random.Next(0, SlideShowElements.Count)];
                            SplashScreenImage = new BitmapImage(new Uri(element.ImageURL));
                            SplashScreenModName = element.ModName;
                            SplashScreenAuthorName = element.AuthorName;
                            SplashScreenSummary = element.ModSummary;
                            _nexusSiteURL = element.ModURL;

                            _lastSlideShowUpdate = DateTime.Now;
                        });
                    }
                }

                Thread.Sleep(1000);
            }
        }

        public bool Running { get; set; } = true;

        internal void ConfigureForInstall(ModList modlist)
        {
            _modList = modlist;
            Mode = "Installing";
            ModListName = _modList.Name;
            HTMLReport = _modList.ReportHTML;
            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            SlideShowElements = modlist.Archives.OfType<NexusMod>().Select(m => new SlideShowItem
            {
                ModName = NexusApiUtils.FixupSummary(m.ModName),
                AuthorName = NexusApiUtils.FixupSummary(m.Author),
                ModSummary = NexusApiUtils.FixupSummary(m.Summary),
                ImageURL = m.SlideShowPic,
                ModURL = m.NexusURL,
            }).ToList();
        }

        public void LogMsg(string msg)
        {
            msg = $"{(DateTime.Now - _startTime).TotalSeconds:0.##} - {msg}";
            dispatcher.Invoke(() => Log.Add(msg));
            lock (dispatcher)
            {
                File.AppendAllText(LogFile, msg + "\r\n");
            }
        }

        public void SetProgress(int id, string msg, int progress)
        {
            lock (InternalStatus)
            {
                Dirty = true;
                while (id >= InternalStatus.Count) InternalStatus.Add(new CPUStatus());

                InternalStatus[id] = new CPUStatus {ID = id, Msg = msg, Progress = progress};
            }
        }

        public void SetQueueSize(int max, int current)
        {
            if (max == 0)
                max = 1;
            var total = current * 100 / max;
            QueueProgress = total;
        }

        private void ExecuteChangePath()
        {
            if (Mode == "Installing")
            {
                var folder = UIUtils.ShowFolderSelectionDialog("Select Installation directory");
                if (folder != null)
                {
                    Location = folder;
                    if (_downloadLocation == null)
                        DownloadLocation = Path.Combine(Location, "downloads");
                }
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

        private void ConfigureForBuild()
        {
            var profile_folder = Path.GetDirectoryName(Location);
            var mo2folder = Path.GetDirectoryName(Path.GetDirectoryName(profile_folder));
            if (!File.Exists(Path.Combine(mo2folder, "ModOrganizer.exe")))
                LogMsg($"Error! No ModOrganizer2.exe found in {mo2folder}");

            var profile_name = Path.GetFileName(profile_folder);
            ModListName = profile_name;
            Mode = "Building";

            var tmp_compiler = new Compiler(mo2folder);
            DownloadLocation = tmp_compiler.MO2DownloadsFolder;

            _mo2Folder = mo2folder;
        }

        private void ShowReport()
        {
            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, HTMLReport);
            Process.Start(file);
        }


        private void ExecuteBegin()
        {
            UIReady = false;
            if (Mode == "Installing")
            {
                var installer = new Installer(_modList, Location);

                installer.DownloadFolder = DownloadLocation;
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
                    }
                });
                th.Priority = ThreadPriority.BelowNormal;
                th.Start();
            }
            else if (_mo2Folder != null)
            {
                var compiler = new Compiler(_mo2Folder);
                compiler.MO2Profile = ModListName;
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
                });
                th.Priority = ThreadPriority.BelowNormal;
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