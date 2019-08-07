using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
            ArchiveFile.SetupLibrary();
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
            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        }

        public void LogMsg(string msg)
        {
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

        private void ConfigureForBuild()
        {
            var profile_folder = Path.GetDirectoryName(Location);
            var mo2folder = Path.GetDirectoryName(Path.GetDirectoryName(profile_folder));
            if (!File.Exists(Path.Combine(mo2folder, "ModOrganizer.exe")))
                LogMsg($"Error! No ModOrganizer2.exe found in {mo2folder}");

            var profile_name = Path.GetFileName(profile_folder);
            ModListName = profile_name;
            Mode = "Building";
            _mo2Folder = mo2folder;
        }

        private ICommand _begin;
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

        private void ExecuteBegin()
        {
            if (Mode == "Installing")
            {
                var installer = new Installer(_modList, Location, msg => this.LogMsg(msg));
                installer.IgnoreMissingFiles = IgnoreMissingFiles;
                var th = new Thread(() =>
                {
                    try
                    {
                        installer.Install();
                    }
                    catch (AggregateException ex)
                    {
                        LogMsg(ex.StackTrace);
                        LogMsg(ex.InnerException.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogMsg(ex.StackTrace);
                        LogMsg(ex.InnerException.ToString());
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
                        compiler.LoadArchives();
                        compiler.Compile();
                    }
                    catch (Exception ex)
                    {
                        LogMsg(ex.StackTrace);
                        LogMsg(ex.InnerException.ToString());
                        LogMsg($"{ex.Message} - Can't continue");
                    }
                });
                th.Priority = ThreadPriority.BelowNormal;
                th.Start();
            }
        }
    }
}