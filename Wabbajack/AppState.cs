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
using System.Windows.Threading;
using Wabbajack.Common;

namespace Wabbajack
{
    internal class AppState : INotifyPropertyChanged
    {
        public class CPUStatus
        {
            public int Progress { get; internal set; }
            public string Msg { get; internal set; }
            public int ID { get; internal set; }
        }

        public volatile bool Dirty;

        private Dispatcher dispatcher;

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ObservableCollection<string> Log { get; }
        public ObservableCollection<CPUStatus> Status { get; }


        private string _mode;
        public string Mode
        {
            get
            {
                return _mode;
            }
            set
            {
                _mode = value;
                OnPropertyChanged("Mode");
            }
        }

        private bool _ignoreMissingFiles = false;
        public bool IgnoreMissingFiles
        {
            get
            {
                return _ignoreMissingFiles;
            }
            set
            {
                if (value)
                {
                    if (MessageBox.Show("Setting this value could result in broken installations. \n Are you sure you want to continue?", "Ignore Missing Files?", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                        == MessageBoxResult.OK)
                    {
                        _ignoreMissingFiles = value;
                    }
                }
                else
                {
                    _ignoreMissingFiles = value;
                }
                OnPropertyChanged("IgnoreMissingFiles");
            }
        }

        private string _mo2Folder;
        private string _modListName;
        private ModList _modList;
        private string _location;
        private string _downloadLocation;

        public string ModListName
        {
            get
            {
                return _modListName;
            }
            set
            {
                _modListName = value;
                OnPropertyChanged("ModListName");
            }
        }

        public string Location
        {
            get
            {
                return _location;
            }
            set
            {
                _location = value;
                OnPropertyChanged("Location");
            }
        }

        public string DownloadLocation
        {
            get
            {
                return _downloadLocation;
            }
            set
            {
                _downloadLocation = value;
                OnPropertyChanged("DownloadLocation");
            }
        }

        private string _htmlReport;
        public Visibility ShowReportButton => _htmlReport == null ? Visibility.Collapsed : Visibility.Visible;

        public string HTMLReport
        {
            get { return _htmlReport; }
            set
            {
                _htmlReport = value;
                OnPropertyChanged("HTMLReport");
                OnPropertyChanged("ShowReportButton");
            }
        }

        private int _queueProgress;
        public int QueueProgress
        {
            get
            {
                return _queueProgress;
            }
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
        public string LogFile { get; private set; }

        public AppState(Dispatcher d, String mode)
        {
            if (Assembly.GetEntryAssembly().Location.ToLower().Contains("\\downloads\\"))
            {
                MessageBox.Show(
                    "This app seems to be running inside a folder called `Downloads`, such folders are often highly monitored by Antivirus software and they can often " +
                    "conflict with the operations Wabbajack needs to perform. Please move this executable outside of your `Downloads` folder and then restart the app.",
                    "Cannot run inside `Downloads`",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }

            if (Directory.EnumerateDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModOrganizer"))
                .Any(f => !f.EndsWith("\\cache")))
            {
                MessageBox.Show(
                    "You seem to have a installed copy of Mod Organizer 2 on your system. Wabbajack requires that Mod Organizer 2 be run in `Portable` mode. " +
                    "Unfortunately it is impossible to have both portable and non-portable versions of MO2 on the same system at the same time. Pleas uninstall Mod Organizer 2 " +
                    "and use only portable (archive) versions with Wabbajack. If you get this message after uninstalling MO2, be sure to delete the folders in `AppData\\Local\\Mod Organizer`",
                    "Cannot run with non-portable MO2 installed.",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(2);
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

        private void UpdateLoop()
        {
            while (true)
            {
                if (Dirty)
                {
                    lock (InternalStatus)
                    {
                        var data = InternalStatus.ToArray();
                        dispatcher.Invoke(() =>
                        {
                            for (int idx = 0; idx < data.Length; idx += 1)
                            {
                                if (idx >= Status.Count)
                                    Status.Add(data[idx]);
                                else if (Status[idx] != data[idx])
                                    Status[idx] = data[idx];
                            }
                        });
                        Dirty = false;
                    }
                }
                Thread.Sleep(1000);
            }
        }

        internal void ConfigureForInstall(string modlist)
        {
            _modList = modlist.FromJSONString<ModList>();
            Mode = "Installing";
            ModListName = _modList.Name;
            HTMLReport = _modList.ReportHTML;
            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        }

        public void LogMsg(string msg)
        {
            msg = $"{(DateTime.Now - _startTime).TotalSeconds:0.##} - {msg}";
            dispatcher.Invoke(() => Log.Add(msg));
            lock (dispatcher) {
                File.AppendAllText(LogFile, msg + "\r\n");
            }
        }

        public void SetProgress(int id, string msg, int progress)
        {
            lock (InternalStatus)
            {
                Dirty = true;
                while (id >= InternalStatus.Count)
                {
                    InternalStatus.Add(new CPUStatus());
                }

                InternalStatus[id] = new CPUStatus() { ID = id, Msg = msg, Progress = progress };
            }
        }

        public void SetQueueSize(int max, int current)
        {
            if (max == 0)
                max = 1;
            var total = current * 100 / max;
            QueueProgress = total;
        }

        private ICommand _changePath;
        public ICommand ChangePath
        {
            get
            {
                if (_changePath == null)
                {
                    _changePath = new LambdaCommand(() => true, () => this.ExecuteChangePath());
                }
                return _changePath;
            }
        }

        private ICommand _changeDownloadPath;
        public ICommand ChangeDownloadPath
        {
            get
            {
                if (_changeDownloadPath == null)
                {
                    _changeDownloadPath = new LambdaCommand(() => true, () => this.ExecuteChangeDownloadPath());
                }
                return _changeDownloadPath;
            }
        }

        private void ExecuteChangePath()
        {
            if (Mode == "Installing")
            {
                var ofd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
                ofd.Description = "Select Installation Directory";
                ofd.UseDescriptionForTitle = true;
                if (ofd.ShowDialog() == true)
                {
                    Location = ofd.SelectedPath;
                }
            }
            else
            {
                var fsd = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
                fsd.Title = "Select a ModOrganizer modlist.txt file";
                fsd.Filter = "modlist.txt|modlist.txt";
                if (fsd.ShowDialog() == true)
                {
                    Location = fsd.FileName;
                    ConfigureForBuild();
                }
            }
        }

        private void ExecuteChangeDownloadPath()
        {
            var ofd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            ofd.Description = "Select a location for MO2 downloads";
            ofd.UseDescriptionForTitle = true;
            if (ofd.ShowDialog() == true)
            {
                DownloadLocation = ofd.SelectedPath;
            }
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

            var tmp_compiler = new Compiler(mo2folder, Utils.Log);
            DownloadLocation = tmp_compiler.MO2DownloadsFolder;

            _mo2Folder = mo2folder;
        }

        private ICommand _begin;
        private DateTime _startTime;

        public ICommand Begin
        {
            get
            {
                if (_begin == null)
                {
                    _begin = new LambdaCommand(() => true, () => this.ExecuteBegin());
                }
                return _begin;
            }
        }

        private ICommand _showReportCommand;
        public ICommand ShowReportCommand
        {
            get
            {
                if (_showReportCommand == null)
                {
                    _showReportCommand = new LambdaCommand(() => true, () => this.ShowReport());
                }
                return _showReportCommand;
            }
        }
        
        private void ShowReport()
        {
            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, HTMLReport);
            Process.Start(file);
        }


        private void ExecuteBegin()
        {
            if (Mode == "Installing")
            {
                var installer = new Installer(_modList, Location, msg => this.LogMsg(msg));
                installer.IgnoreMissingFiles = IgnoreMissingFiles;
                installer.DownloadFolder = DownloadLocation;
                var th = new Thread(() =>
                {
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
                });
                th.Priority = ThreadPriority.BelowNormal;
                th.Start();
            }
            else
            {
                var compiler = new Compiler(_mo2Folder, msg => LogMsg(msg));
                compiler.IgnoreMissingFiles = IgnoreMissingFiles;
                compiler.MO2Profile = ModListName;
                var th = new Thread(() =>
                {
                    try
                    {
                        compiler.Compile();
                        if (compiler.ModList != null && compiler.ModList.ReportHTML != null)
                        {
                            HTMLReport = compiler.ModList.ReportHTML;
                        }
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        LogMsg(ex.StackTrace);
                        LogMsg(ex.ToString());
                        LogMsg($"{ex.Message} - Can't continue");
                    }
                });
                th.Priority = ThreadPriority.BelowNormal;
                th.Start();
            }
        }
    }
}