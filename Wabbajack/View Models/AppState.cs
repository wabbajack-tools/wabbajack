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

namespace Wabbajack
{
    public class AppState : ViewModel, IDataErrorInfo
    {
        public SlideShow Slideshow { get; }
        public MainWindowVM MWVM { get; }

        private string _mo2Folder;

        public readonly BitmapImage _noneImage = UIUtils.BitmapImageFromResource("Wabbajack.Resources.none.jpg");

        private ModList _ModList;
        public ModList ModList { get => _ModList; private set => this.RaiseAndSetIfChanged(ref _ModList, value); }

        private string _ModListPath;
        public string ModListPath { get => _ModListPath; private set => this.RaiseAndSetIfChanged(ref _ModListPath, value); }

        private RunMode _Mode;
        public RunMode Mode { get => _Mode; private set => this.RaiseAndSetIfChanged(ref _Mode, value); }

        private string _ModListName;
        public string ModListName { get => _ModListName; set => this.RaiseAndSetIfChanged(ref _ModListName, value); }

        private bool _UIReady;
        public bool UIReady { get => _UIReady; set => this.RaiseAndSetIfChanged(ref _UIReady, value); }

        private string _HTMLReport;
        public string HTMLReport { get => _HTMLReport; set => this.RaiseAndSetIfChanged(ref _HTMLReport, value); }

        private bool _Installing;
        public bool Installing { get => _Installing; set => this.RaiseAndSetIfChanged(ref _Installing, value); }

        // Command properties
        public IReactiveCommand ChangePathCommand { get; }
        public IReactiveCommand ChangeDownloadPathCommand { get; }
        public IReactiveCommand BeginCommand { get; }
        public IReactiveCommand ShowReportCommand { get; }
        public IReactiveCommand OpenReadmeCommand { get; }
        public IReactiveCommand OpenModListPropertiesCommand { get; }

        public AppState(MainWindowVM mainWindowVM, RunMode mode)
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
            Mode = mode;

            // Define commands
            this.ChangePathCommand = ReactiveCommand.Create(ExecuteChangePath);
            this.ChangeDownloadPathCommand = ReactiveCommand.Create(ExecuteChangeDownloadPath);
            this.ShowReportCommand = ReactiveCommand.Create(ShowReport);
            this.OpenModListPropertiesCommand = ReactiveCommand.Create(
                execute: OpenModListProperties,
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread());
            this.OpenReadmeCommand = ReactiveCommand.Create(
                execute: this.OpenReadmeWindow,
                canExecute: this.WhenAny(x => x.ModList)
                    .Select(modList => !string.IsNullOrEmpty(modList?.Readme))
                    .ObserveOnGuiThread());
            this.BeginCommand = ReactiveCommand.Create(
                execute: this.ExecuteBegin,
                canExecute: this.WhenAny(x => x.UIReady)
                    .ObserveOnGuiThread());

            this.Slideshow = new SlideShow(this);
        }

        public ObservableCollection<string> Log { get; } = new ObservableCollection<string>();

        private string _Location;
        public string Location { get => _Location; set => this.RaiseAndSetIfChanged(ref _Location, value); }

        private string _LocationLabel;
        public string LocationLabel { get => _LocationLabel; set => this.RaiseAndSetIfChanged(ref _LocationLabel, value); }

        private string _DownloadLocation;
        public string DownloadLocation { get => _DownloadLocation; set => this.RaiseAndSetIfChanged(ref _DownloadLocation, value); }

        public string LogFile { get; }

        private void ExecuteChangePath()
        {
            switch (this.Mode)
            {
                case RunMode.Compile:
                    Location = UIUtils.ShowFolderSelectionDialog("Select Your MO2 profile directory");
                    break;
                case RunMode.Install:
                    var folder = UIUtils.ShowFolderSelectionDialog("Select Installation directory");
                    if (folder == null) return;
                    Location = folder;
                    if (DownloadLocation == null)
                    {
                        DownloadLocation = Path.Combine(Location, "downloads");
                    }
                    break;
                default:
                    throw new NotImplementedException();
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

        private void OpenReadmeWindow()
        {
            if (string.IsNullOrEmpty(this.ModList.Readme)) return;
            using (var fs = new FileStream(this.ModListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            using (var ms = new MemoryStream())
            {
                var entry = ar.GetEntry(this.ModList.Readme);
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
                        case RunMode.Compile when Location != null && Directory.Exists(Location) && File.Exists(Path.Combine(Location, "modlist.txt")):
                            Location = Path.Combine(Location, "modlist.txt");
                            validationMessage = null;
                            ConfigureForBuild();
                            break;
                        case RunMode.Install when Location != null && Directory.Exists(Location) && !Directory.EnumerateFileSystemEntries(Location).Any():
                            validationMessage = null;
                            break;
                        case RunMode.Install when Location != null && Directory.Exists(Location) && Directory.EnumerateFileSystemEntries(Location).Any():
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

        public void LogMsg(string msg)
        {
            Application.Current.Dispatcher.Invoke(() => Log.Add(msg));
        }

        private void ConfigureForBuild()
        {
            var profile_folder = Path.GetDirectoryName(Location);
            var mo2folder = Path.GetDirectoryName(Path.GetDirectoryName(profile_folder));
            if (!File.Exists(Path.Combine(mo2folder, "ModOrganizer.exe")))
                LogMsg($"Error! No ModOrganizer2.exe found in {mo2folder}");

            var profile_name = Path.GetFileName(profile_folder);
            this.ModListName = profile_name;
            this.Mode = RunMode.Compile;

            var tmp_compiler = new Compiler(mo2folder);
            DownloadLocation = tmp_compiler.MO2DownloadsFolder;

            _mo2Folder = mo2folder;
        }

        internal void ConfigureForInstall(string source, ModList modlist)
        {
            this.ModList = modlist;
            this.ModListPath = source;
            this.Mode = RunMode.Install;
            ModListName = this.ModList.Name;
            HTMLReport = this.ModList.ReportHTML;
            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            this.Slideshow.SlideShowElements = modlist.Archives
                .Select(m => m.State)
                .OfType<NexusDownloader.State>()
                .Select(m => 
                new Slide(NexusApiUtils.FixupSummary(m.ModName),m.ModID,
                    NexusApiUtils.FixupSummary(m.Summary), NexusApiUtils.FixupSummary(m.Author),
                    m.Adult,m.NexusURL,m.SlideShowPic)).ToList();


            this.Slideshow.PreloadSlideShow();
        }

        private void ExecuteBegin()
        {
            UIReady = false;
            if (this.Mode == RunMode.Install)
            {
                this.Installing = true;
                var installer = new Installer(this.ModListPath, this.ModList, Location)
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
                        this.Installing = false;
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
                    ModListName = ChangedProperties ? this.Slideshow.ModName : null,
                    ModListAuthor = ChangedProperties ? this.Slideshow.AuthorName : null,
                    ModListDescription = ChangedProperties ? this.Slideshow.Summary : null,
                    ModListImage = ChangedProperties ? newImagePath : null,
                    ModListWebsite = ChangedProperties ? this.Slideshow.NexusSiteURL : null,
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
}