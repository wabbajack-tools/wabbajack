using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Common;

namespace Wabbajack
{
    public class MO2InstallerConfigVM : ViewModel
    {
        private InstallationConfigVM _installationConfig;

        [Reactive]
        public ModListVM Modlist { get; set; }

        [Reactive]
        public bool EnableBegin { get; set; }

        [Reactive]
        public string ModListName { get; set; }

        [Reactive]
        public string ModListAuthor { get; set; }

        [Reactive]
        public string ModListImage { get; set; }

        [Reactive]
        public string InstallationPath { get; set; }

        private readonly ObservableAsPropertyHelper<IErrorResponse> _installationPathError;
        public IErrorResponse InstallationPathError => _installationPathError.Value;

        [Reactive]
        public string DownloadPath { get; set; }

        private readonly ObservableAsPropertyHelper<IErrorResponse> _downloadPathError;
        public IErrorResponse DownloadPathError => _downloadPathError.Value;

        public IReactiveCommand BeginCommand { get; }
        public IReactiveCommand ViewReadmeCommand { get; }
        public IReactiveCommand VisitWebsiteCommand { get; }
        public IReactiveCommand ViewManifestCommand { get; }

        public MO2InstallerConfigVM(InstallationConfigVM installationConfig)
        {
            _installationConfig = installationConfig;

            this.WhenAny(x => x.Modlist).Subscribe(modlist =>
            {
                if(modlist == null) return;
                ModListName = modlist.Name;
                ModListAuthor = $"by {modlist.Author}";
                ModListImage = string.IsNullOrEmpty(modlist.ImageURL) ? "https://raw.githubusercontent.com/wabbajack-tools/wabbajack/master/Wabbajack/Resources/none.jpg" : Modlist.ImageURL;
            }).DisposeWith(CompositeDisposable);

            _installationPathError = this.WhenAny(x => x.InstallationPath)
                .Select(Utils.IsDirectoryPathValid)
                .ToProperty(this, nameof(InstallationPathError));

            _downloadPathError = this.WhenAny(x => x.DownloadPath)
                .Select(Utils.IsDirectoryPathValid)
                .ToProperty(this, nameof(DownloadPathError));

            this.WhenAny(x => x.InstallationPath)
                .Skip(1).Subscribe(path =>
                {
                    if (!string.IsNullOrWhiteSpace(DownloadPath)) return;
                    DownloadPath = Path.Combine(path, "downloads");
                    EnableBegin = true;
                }).DisposeWith(CompositeDisposable);

            this.WhenAny(x => x.DownloadPath).Subscribe(path =>
            {
                if (string.IsNullOrWhiteSpace(DownloadPath) || !Utils.IsDirectoryPathValid(path).Succeeded) 
                    EnableBegin = false;
            }).DisposeWith(CompositeDisposable);

            //Commands
            ViewReadmeCommand = ReactiveCommand.Create(
                OpenReadmeWindow,
                this.WhenAny(x => x.Modlist)
                    .Select(modList => !string.IsNullOrEmpty(modList?.Readme))
                    .ObserveOnGuiThread());
            ViewManifestCommand = ReactiveCommand.Create(ShowReport);
            VisitWebsiteCommand = ReactiveCommand.Create(
                () => Process.Start(Modlist.Website),
                this.WhenAny(x => x.Modlist.Website)
                    .Select(x => x?.StartsWith("https://") ?? false)
                    .ObserveOnGuiThread());

            //begin only if there are no path errors
            BeginCommand = ReactiveCommand.Create(
                _installationConfig.Install,
                this.WhenAny(x => x.InstallationPathError).CombineLatest(this.WhenAny(x => x.DownloadPathError),
                        (iPath, dPath) => (iPath?.Succeeded ?? false) && (dPath?.Succeeded ?? false))
                    .ObserveOnGuiThread());
        }

        private void OpenReadmeWindow()
        {
            if (string.IsNullOrEmpty(Modlist.Readme)) return;
            using (var fs = new FileStream(Modlist.ModListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            using (var ms = new MemoryStream())
            {
                var entry = ar.GetEntry(Modlist.Readme);
                if (entry == null)
                {
                    Utils.Log($"Tried to open a non-existant readme: {Modlist.Readme}");
                    return;
                }
                using (var e = entry.Open())
                {
                    e.CopyTo(ms);
                }
                ms.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(ms))
                {
                    var viewer = new TextViewer(reader.ReadToEnd(),Modlist.Name);
                    viewer.Show();
                }
            }
        }

        private void ShowReport()
        {
            var report = Path.GetTempFileName() + ".html";
            File.WriteAllText(report, Modlist.ReportHTML);
            Process.Start(report);
        }
    }
}
