using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

namespace Wabbajack
{
    public enum TaskMode { INSTALLING, BUILDING }
    internal class AppState : ViewModel, IDataErrorInfo
    {
        private const int MAX_CACHE_SIZE = 10;

        private string _mo2Folder;

        private ModList _modList;

        private readonly DateTime _startTime;

        public volatile bool Dirty;

        private readonly Dispatcher dispatcher;

        public AppState(Dispatcher d, TaskMode mode)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.UI.banner.png");
            image.EndInit();
            _wabbajackLogo = image;
            _splashScreenImage = image;

            image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.UI.none.jpg");
            image.EndInit();
            _noneImage = image;

            image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.UI.Icons.next.png");
            image.EndInit();
            _nextIcon = image;

            SetupSlideshow();

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

            var th = new Thread(() => UpdateLoop())
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
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

        private int _QueueProgress;
        public int QueueProgress { get => _QueueProgress; set => this.RaiseAndSetIfChanged(ref _QueueProgress, value); }

        private List<CPUStatus> InternalStatus { get; } = new List<CPUStatus>();
        public string LogFile { get; }

        private ICommand _changePath;
        public ICommand ChangePath
        {
            get
            {
                if (_changePath == null) _changePath = new LambdaCommand(() => true, () => ExecuteChangePath());
                return _changePath;
            }
        }

        private ICommand _changeDownloadPath;
        public ICommand ChangeDownloadPath
        {
            get
            {
                if (_changeDownloadPath == null)
                    _changeDownloadPath = new LambdaCommand(() => true, () => ExecuteChangeDownloadPath());
                return _changeDownloadPath;
            }
        }

        private ICommand _begin;
        public ICommand Begin
        {
            get
            {
                if (_begin == null) _begin = new LambdaCommand(() => true, () => ExecuteBegin());
                return _begin;
            }
        }

        private ICommand _showReportCommand;
        public ICommand ShowReportCommand
        {
            get
            {
                if (_showReportCommand == null) _showReportCommand = new LambdaCommand(() => true, () => ShowReport());
                return _showReportCommand;
            }
        }

        private ICommand _visitNexusSiteCommand;
        public ICommand VisitNexusSiteCommand
        {
            get
            {
                if (_visitNexusSiteCommand == null) _visitNexusSiteCommand = new LambdaCommand(() => true, () => VisitNexusSite());
                return _visitNexusSiteCommand;
            }
        }

        public ICommand OpenModListPropertiesCommand
        {
            get
            {
                return new LambdaCommand(() => true, () => OpenModListProperties());
            }
        }

        public ICommand SlideShowNextItem
        {
            get
            {
                return new LambdaCommand(() => true, () => {
                    UpdateSlideShowItem(false);
                });
            }
        }

        private void ExecuteChangePath()
        {
            if (Mode == TaskMode.INSTALLING)
            {
                var folder = UIUtils.ShowFolderSelectionDialog("Select Installation directory");
                if (folder != null)
                {
                    Location = folder;
                    if (DownloadLocation == null)
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
        internal string newImagePath;
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


        private bool _uiReady = false;
        public bool UIReady
        {
            get => _uiReady;
            set => this.RaiseAndSetIfChanged(ref _uiReady, value);
        }

        private readonly BitmapImage _wabbajackLogo = null;
        private readonly BitmapImage _noneImage = null;
        private bool _originalImage = true;
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
        internal Thread slideshowThread = null;
        internal int slideshowSleepTime = 1000;
        private bool _enableSlideShow = true;
        public bool EnableSlideShow
        {
            get => _enableSlideShow;
            set
            {
                RaiseAndSetIfChanged(ref _enableSlideShow, value);
                if (!_enableSlideShow)
                {
                    if (slideshowThread.IsAlive)
                    {
                        ApplyModlistProperties();
                    }
                }
                else
                {
                    UpdateSlideShowItem(false);
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
                    else if (Mode == TaskMode.BUILDING && Location != null && Directory.Exists(Location) && File.Exists(Path.Combine(Location, "modlist.txt")))
                    {
                        Location = Path.Combine(Location, "modlist.txt");
                        validationMessage = null;
                        ConfigureForBuild();
                    }
                    else if (Mode == TaskMode.INSTALLING && Location != null && Directory.Exists(Location) && !Directory.EnumerateFileSystemEntries(Location).Any())
                    {
                        validationMessage = null;
                    }
                    else if (Mode == TaskMode.INSTALLING && Location != null && Directory.Exists(Location) && Directory.EnumerateFileSystemEntries(Location).Any())
                    {
                        validationMessage = "You have selected a non-empty directory. Installing the modlist here might result in a broken install!";
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

                if (slidesQueue.Any())
                {
                    if (DateTime.Now - _lastSlideShowUpdate > TimeSpan.FromSeconds(10))
                    {
                        UpdateSlideShowItem(true);
                    }
                }
                Thread.Sleep(1000);
            }
        }
        private void UpdateSlideShowItem(bool fromLoop)
        {
            if (EnableSlideShow)
            {
                SlideShowItem element = slidesQueue.Peek();
                SplashScreenImage = _noneImage;
                if (element.ImageURL != null)
                {
                    string cachePath = Path.Combine(SlideshowCacheDir, element.ModID + ".slideshowcache");
                    // max cached files achieved
                    if(cachedSlides.Count >= MAX_CACHE_SIZE) { 
                        do
                        {
                            // delete a random file to make room
                            // file can not be in the queue
                            var idx = _random.Next(0, SlideShowElements.Count);
                            var randomElement = SlideShowElements[idx];
                            string randomPath = Path.Combine(SlideshowCacheDir, randomElement.ModID + ".slideshowcache");
                            while (!File.Exists(randomPath)
                                || slidesQueue.Contains(randomElement))
                            {
                                idx = _random.Next(0, SlideShowElements.Count);
                                randomElement = SlideShowElements[idx];
                                randomPath = Path.Combine(SlideshowCacheDir, randomElement.ModID + ".slideshowcache");
                            }

                            if (File.Exists(randomPath) && !IsFileLocked(randomPath))
                            {
                                File.Delete(randomPath);
                                cachedSlides.RemoveAt(cachedSlides.IndexOf(randomElement.ModID));
                            }
                        } while (cachedSlides.Count >= MAX_CACHE_SIZE);
                    }
                    if (!element.Adult || (element.Adult && SplashShowNSFW))
                    {
                        dispatcher.Invoke(() => {
                            var data = new MemoryStream();
                            if (!IsFileLocked(cachePath) && File.Exists(cachePath))
                            {
                                using (var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read))
                                    stream.CopyTo(data);

                                data.Seek(0, SeekOrigin.Begin);

                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.StreamSource = data;
                                bitmap.EndInit();

                                SplashScreenImage = bitmap;

                            }
                        });
                    }
                }
                _originalImage = false;
                SplashScreenModName = element.ModName;
                SplashScreenAuthorName = element.AuthorName;
                SplashScreenSummary = element.ModSummary;
                _nexusSiteURL = element.ModURL;

                if (fromLoop)
                    _lastSlideShowUpdate = DateTime.Now;

                slidesQueue.Dequeue();
                QueueRandomSlide(false, true);
            }
        }

        private string SlideshowCacheDir;
        private List<string> cachedSlides;
        private Queue<SlideShowItem> slidesQueue;
        private SlideShowItem lastSlide;
        /// <summary>
        /// Caches a slide
        /// </summary>
        /// <param name="url">The url</param>
        /// <param name="dest">The destination</param>
        private void CacheSlide(string url, string dest)
        {
            bool sync = false;
            using (var file = new FileStream(dest, FileMode.Create, FileAccess.Write))
            {
                if (sync)
                {
                    dispatcher.Invoke(() =>
                    {
                        using (var stream = new HttpClient().GetStreamSync(url))
                            stream.CopyTo(file);
                    });
                }
                else
                {
                    using (var stream = new HttpClient().GetStreamAsync(url))
                    {
                        stream.Wait();
                        stream.Result.CopyTo(file);
                    }
                }
            }
        }
        /// <summary>
        /// Queues a random slide
        /// </summary>
        /// <param name="init">Only used if called from the Preloadfunction</param>
        /// <param name="checkLast">If to not queue the same thing again</param>
        /// <returns></returns>
        private bool QueueRandomSlide(bool init, bool checkLast)
        {
            bool result = false;
            var idx = _random.Next(0, SlideShowElements.Count);
            var element = SlideShowElements[idx];
            if (checkLast)
            {
                while(element == lastSlide && (!element.Adult || (element.Adult && SplashShowNSFW)))
                {
                    idx = _random.Next(0, SlideShowElements.Count);
                    element = SlideShowElements[idx];
                }
            }

            string id = element.ModID;
            string cacheFile = Path.Combine(SlideshowCacheDir, id + ".slideshowcache");

            if(element.ImageURL == null)
            {
                if(!init)
                    slidesQueue.Enqueue(element);
            }
            else
            {
                // if the file doesen't exist, we cache it and add it to the cachedSlide list and to the 
                // slidesQueue
                // return true for the PreloadSlideshow
                if (!File.Exists(cacheFile))
                {
                    CacheSlide(element.ImageURL, cacheFile);
                    cachedSlides.Add(id);
                    slidesQueue.Enqueue(element);
                    result = true;
                }
                // if the file exists and was not called from preloadslideshow, we queue the slide
                else
                {
                    if (!init)
                        slidesQueue.Enqueue(element);
                }
                // set the last element to the current element after queueing
                lastSlide = element;
            }
            return result;
        }
        /// <summary>
        /// Caches 3 random images and puts them in the queue
        /// </summary>
        private void PreloadSlideshow()
        {
            cachedSlides = new List<string>();
            slidesQueue = new Queue<SlideShowItem>();

            if (!Directory.Exists(SlideshowCacheDir))
                Directory.CreateDirectory(SlideshowCacheDir);

            int turns = 0;
            for(int i = 0; i < SlideShowElements.Count; i++)
            {
                if (turns >= 3)
                    break;

                if (QueueRandomSlide(true, false))
                    turns++;
                else
                    continue;
            }
        }
        /// <summary>
        /// Deletes the slideshow cache
        /// </summary>
        private void DeleteCache()
        {
            if (Directory.Exists(SlideshowCacheDir))
            {
                foreach (string s in Directory.GetFiles(SlideshowCacheDir))
                    File.Delete(s);
                //Directory.Delete(SlideshowCacheDir);
            }
        }

        public bool Running { get; set; } = true;
        private void ApplyModlistProperties()
        {
            SplashScreenModName = _modList.Name;
            SplashScreenAuthorName = _modList.Author;
            _nexusSiteURL = _modList.Website;
            SplashScreenSummary = _modList.Description;
            if (_modList.Image != null)
            {
                //TODO: if(_modList.Image != null) SplashScreenImage = _modList.Image;
                SplashScreenImage = _wabbajackLogo;
            }
            else
            {
                if (!_originalImage) {
                    SplashScreenImage = _wabbajackLogo;
                }
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
            SlideshowCacheDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "slideshow_cache");
            DeleteCache();

            _modList = modlist;
            _modListPath = source;
            Mode = TaskMode.INSTALLING;
            ModListName = _modList.Name;
            HTMLReport = _modList.ReportHTML;
            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ApplyModlistProperties();

            SlideShowElements = modlist.Archives.OfType<NexusMod>().Select(m => new SlideShowItem
            {
                ModName = NexusApiUtils.FixupSummary(m.ModName),
                AuthorName = NexusApiUtils.FixupSummary(m.Author),
                ModSummary = NexusApiUtils.FixupSummary(m.Summary),
                ImageURL = m.SlideShowPic,
                ModURL = m.NexusURL,
                Adult = m.Adult,
                ModID = m.ModID
            }).ToList();


            PreloadSlideshow();
        }

        private void ExecuteBegin()
        {
            UIReady = false;
            if (Mode == TaskMode.INSTALLING)
            {
                slideshowThread = new Thread(() => UpdateLoop())
                {
                    Priority = ThreadPriority.BelowNormal,
                    IsBackground = true
                };
                slideshowThread.Start();
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
                        DeleteCache();
                        Running = false;
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
                    ModListImage = ChangedProperties ? newImagePath ?? null : null,
                    ModListWebsite = ChangedProperties ? _nexusSiteURL : null
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

        private bool IsFileLocked(string path)
        {
            bool result = false;
            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    result = false;
                }
            }
            catch (IOException) { result = true; }
            return result;
        }
    }

    public class CPUStatus
    {
        public int Progress { get; internal set; }
        public string Msg { get; internal set; }
        public int ID { get; internal set; }
    }
}