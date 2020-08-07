using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Launcher.Annotations;

namespace Wabbajack.Launcher
{
    public class MainWindowVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private WebClient _client = new WebClient();
        public Uri GITHUB_REPO = new Uri("https://api.github.com/repos/wabbajack-tools/wabbajack/releases");


        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _status = "Checking for Updates";
        private Release _version;

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
            _client.Headers.Add ("user-agent", "Wabbajack Launcher");
            Status = "Selecting Release";

            try
            {
                var releases = await GetReleases();
                _version = releases.OrderByDescending(r =>
                {
                    if (r.Tag.Split(".").Length == 4 && Version.TryParse(r.Tag, out var v))
                        return v;
                    return new Version(0, 0, 0, 0);
                }).FirstOrDefault();
            }
            catch (Exception)
            {
                FinishAndExit();
            }
            
            if (_version == null)
                FinishAndExit();

            Status = "Looking for Updates";
            
            var base_folder = Path.Combine(Directory.GetCurrentDirectory(), _version.Tag);
            
            if (File.Exists(Path.Combine(base_folder, "Wabbajack.exe")))
                FinishAndExit();

            var asset = _version.Assets.FirstOrDefault(a => a.Name == _version.Tag + ".zip");
            if (asset == null)
                FinishAndExit();

            var wc = new WebClient();
            wc.DownloadProgressChanged += UpdateProgress;
            Status = $"Downloading {_version.Tag} ...";
            var data = await wc.DownloadDataTaskAsync(asset.BrowserDownloadUrl);
            
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
            var wjFolder = Directory.EnumerateDirectories(Directory.GetCurrentDirectory())
                .OrderByDescending(v =>
                    Version.TryParse(Path.GetFileName(v), out var ver) ? ver : new Version(0, 0, 0, 0))
                .FirstOrDefault();
            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(wjFolder, "Wabbajack.exe"), 
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(s => s.Contains(' ') ? '\"' + s + '\"' : s)),
                WorkingDirectory = wjFolder,
            };
            Process.Start(info);
            Environment.Exit(0);
        }

        private void UpdateProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            Status = $"Downloading {_version.Tag} ({e.ProgressPercentage}%)...";
        }

        private async Task<Release[]> GetReleases()
        {
            Status = "Checking GitHub Repository";
            var data = await _client.DownloadStringTaskAsync(GITHUB_REPO);
            Status = "Parsing Response";
            return JsonConvert.DeserializeObject<Release[]>(data);
        }


        class Release
        {
            [JsonProperty("tag_name")]
            public string Tag { get; set; }
            
            [JsonProperty("assets")]
            public Asset[] Assets { get; set; }
        }

        class Asset
        {
            [JsonProperty("browser_download_url")]
            public Uri BrowserDownloadUrl { get; set; }
            
            [JsonProperty("name")]
            public string Name { get; set; }
        }
    }
}
