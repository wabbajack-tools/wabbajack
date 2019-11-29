using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Alphaleonis.Win32.Filesystem;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.View_Models
{
    public enum DownloadStatus
    {
        NotDownloaded,
        Downloading,
        Downloaded
    }
    public class ModListMetadataVM : ViewModel
    {


        public ModlistMetadata Metadata { get; }
        private ModeSelectionVM _parent;


        public ModListMetadataVM(ModeSelectionVM parent, ModlistMetadata metadata)
        {
            _parent = parent;
            Metadata = metadata;
            Click = ReactiveCommand.Create(() => this.DoClick());
        }

        private void DoClick()
        {
            switch (Status)
            {
                case DownloadStatus.NotDownloaded:
                    Download();
                    break;
                case DownloadStatus.Downloading:
                    break;
                case DownloadStatus.Downloaded:
                    Install();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Install()
        {
            _parent.OpenInstaller(Location);
        }

        private void Download()
        {
            IsDownloading = true;

            var queue = new WorkQueue(1);
            DownloadStatusText = "Downloading";
            var sub = queue.Status.Select(i => i.ProgressPercent).Subscribe(v => DownloadProgress = v);
            queue.QueueTask(() =>
            {
                var downloader = DownloadDispatcher.ResolveArchive(Metadata.Links.Download);
                downloader.Download(new Archive{ Name = Metadata.Title, Size = Metadata.DownloadMetadata?.Size ?? 0}, Location);
                DownloadStatusText = "Hashing";
                Location.FileHashCached();
                IsDownloading = false;
                sub.Dispose();
            });
        }

        private void UpdateDownloadStatuses()
        {
            this.RaisePropertyChanged("Status");
            this.RaisePropertyChanged("DownloadButtonVisibility");
            this.RaisePropertyChanged("DownloadProgressVisibility");
            this.RaisePropertyChanged("InstallButtonVisibility");
        }

        public string Location => Path.Combine(Consts.ModListDownloadFolder, Metadata.Links.MachineURL + ExtensionManager.Extension);

        private bool _isDownloading = false;
        public bool IsDownloading
        {
            get => _isDownloading;
            private set
            {
                RaiseAndSetIfChanged(ref _isDownloading, value);
                UpdateDownloadStatuses();
            }
        }


        private float _downloadProgress;

        public float DownloadProgress
        {
            get => _downloadProgress;
            private set
            {
                RaiseAndSetIfChanged(ref _downloadProgress, value);
            }
        }

        private string _downloadStatusText;
        public string DownloadStatusText
        {
            get => _downloadStatusText;
            private set
            {
                RaiseAndSetIfChanged(ref _downloadStatusText, value);
            }
        }

        public DownloadStatus Status
        {

            get
            {
                if (IsDownloading) return DownloadStatus.Downloading;
                if (!File.Exists(Location)) return DownloadStatus.NotDownloaded;
                return Metadata.NeedsDownload(Location) ? DownloadStatus.NotDownloaded : DownloadStatus.Downloaded;
            }
        }

        public Visibility DownloadButtonVisibility => Status == DownloadStatus.NotDownloaded ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DownloadProgressVisibility => Status == DownloadStatus.Downloading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InstallButtonVisibility => Status == DownloadStatus.Downloaded ? Visibility.Visible : Visibility.Collapsed;

        public ICommand Click { get; }

    }
}
