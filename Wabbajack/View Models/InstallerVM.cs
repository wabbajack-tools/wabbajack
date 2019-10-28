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
    public class InstallerVM : ViewModel, IDataErrorInfo
    {
        public SlideShow Slideshow { get; }
        public MainWindowVM MWVM { get; }

        private readonly ObservableAsPropertyHelper<ModList> _ModList;
        public ModList ModList => _ModList.Value;

        private string _ModListPath;
        public string ModListPath { get => _ModListPath; private set => this.RaiseAndSetIfChanged(ref _ModListPath, value); }

        public RunMode Mode => RunMode.Install;

        private readonly ObservableAsPropertyHelper<string> _ModListName;
        public string ModListName => _ModListName.Value;

        private bool _UIReady;
        public bool UIReady { get => _UIReady; set => this.RaiseAndSetIfChanged(ref _UIReady, value); }

        private readonly ObservableAsPropertyHelper<string> _HTMLReport;
        public string HTMLReport => _HTMLReport.Value;

        private bool _Installing;
        public bool Installing { get => _Installing; set => this.RaiseAndSetIfChanged(ref _Installing, value); }

        private string _Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public string Location { get => _Location; set => this.RaiseAndSetIfChanged(ref _Location, value); }

        private string _DownloadLocation;
        public string DownloadLocation { get => _DownloadLocation; set => this.RaiseAndSetIfChanged(ref _DownloadLocation, value); }

        // Command properties
        public IReactiveCommand ChangePathCommand { get; }
        public IReactiveCommand ChangeDownloadPathCommand { get; }
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
                .ToProperty(this, nameof(this.ModList));
            this._HTMLReport = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.ReportHTML)
                .ToProperty(this, nameof(this.HTMLReport));
            this._ModListName = this.WhenAny(x => x.ModList)
                .Select(modList => modList?.Name)
                .ToProperty(this, nameof(this.ModListName));

            // Define commands
            this.ChangePathCommand = ReactiveCommand.Create(ExecuteChangePath);
            this.ChangeDownloadPathCommand = ReactiveCommand.Create(ExecuteChangeDownloadPath);
            this.ShowReportCommand = ReactiveCommand.Create(ShowReport);
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

        private void ExecuteChangePath()
        {
            var folder = UIUtils.ShowFolderSelectionDialog("Select Installation directory");
            if (folder == null) return;
            Location = folder;
            if (DownloadLocation == null)
            {
                DownloadLocation = Path.Combine(Location, "downloads");
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
                        Utils.Log(ex.StackTrace);
                        Utils.Log(ex.ToString());
                        Utils.Log($"{ex.Message} - Can't continue");
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
        }

        public void Init(string source)
        {
            this.ModListPath = source;
            this.UIReady = true;
        }
    }
}