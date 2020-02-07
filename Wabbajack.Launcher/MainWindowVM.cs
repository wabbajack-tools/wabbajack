using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Wabbajack.Launcher.Annotations;

namespace Wabbajack.Launcher
{
    public class MainWindowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _status = "Checking for Updates";
        private string _version;

        public string Status
        {
            set
            {
                _status = value;
                OnPropertyChanged("Status");
            }
            get
            {
                return _status;
            }
        }

        public MainWindowVM()
        {
            Task.Run(CheckForUpdates);
        }

        private async Task CheckForUpdates()
        {
            _version = "0.9.17.0";

            var base_folder = Path.Combine(Directory.GetCurrentDirectory());
            
            if (File.Exists(Path.Combine(base_folder, _version, "Wabbajack.exe")))
                FinishAndExit();

            var wc = new WebClient();
            wc.DownloadProgressChanged += UpdateProgress;
            var data = await wc.DownloadDataTaskAsync(new Uri("https://build.wabbajack.org/0.9.17.0.zip"));
            
            using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    Status = $"Extracting: {entry.Name}";
                    var outPath = Path.Combine(base_folder, entry.FullName);
                    if (!Directory.Exists(Path.GetDirectoryName(outPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        continue;
                    await using var o = entry.Open();
                    await using var of = File.Create(outPath);
                    await o.CopyToAsync(of);
                }
            }
            FinishAndExit();
        }

        private void FinishAndExit()
        {
            Status = "Launching...";
            var wj_folder = Path.Combine(Directory.GetCurrentDirectory(), _version);
            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(wj_folder, "Wabbajack.exe"), 
                WorkingDirectory = wj_folder,
            };
            Process.Start(info);
            Environment.Exit(0);
        }

        private void UpdateProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            Status = $"Downloading ({e.ProgressPercentage}%)...";
        }
    }
}
