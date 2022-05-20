using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack;
using Wabbajack.Extensions;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack
{

    public struct ModListTag
    {
        public ModListTag(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public class ModListMetadataVM : ViewModel
    {
        public ModlistMetadata Metadata { get; }
        private ModListGalleryVM _parent;

        public ICommand OpenWebsiteCommand { get; }
        public ICommand ExecuteCommand { get; }
        
        public ICommand ModListContentsCommend { get; }

        private readonly ObservableAsPropertyHelper<bool> _Exists;
        public bool Exists => _Exists.Value;

        public AbsolutePath Location { get; }

        public LoadingLock LoadingImageLock { get; } = new();

        [Reactive]
        public List<ModListTag> ModListTagList { get; private set; }

        [Reactive]
        public Percent ProgressPercent { get; private set; }

        [Reactive]
        public bool IsBroken { get; private set; }
        
        [Reactive]
        public ModListStatus Status { get; set; }
        
        [Reactive]
        public bool IsDownloading { get; private set; }

        [Reactive]
        public string DownloadSizeText { get; private set; }

        [Reactive]
        public string InstallSizeText { get; private set; }
        
        [Reactive]
        public string VersionText { get; private set; }

        [Reactive]
        public IErrorResponse Error { get; private set; }

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        private readonly ObservableAsPropertyHelper<bool> _LoadingImage;
        public bool LoadingImage => _LoadingImage.Value;

        private Subject<bool> IsLoadingIdle;
        private readonly ILogger _logger;
        private readonly ModListDownloadMaintainer _maintainer;
        private readonly Client _wjClient;

        public ModListMetadataVM(ILogger logger, ModListGalleryVM parent, ModlistMetadata metadata,
            ModListDownloadMaintainer maintainer, Client wjClient)
        {
            _logger = logger;
            _parent = parent;
            _maintainer = maintainer;
            Metadata = metadata;
            _wjClient = wjClient;
            Location = LauncherUpdater.CommonFolder.Value.Combine("downloaded_mod_lists", Metadata.NamespacedName).WithExtension(Ext.Wabbajack);
            ModListTagList = new List<ModListTag>();
            
            UpdateStatus().FireAndForget();

            Metadata.Tags.ForEach(tag =>
            {
                ModListTagList.Add(new ModListTag(tag));
            });
            ModListTagList.Add(new ModListTag(metadata.Game.MetaData().HumanFriendlyGameName));

            DownloadSizeText = "Download size : " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfArchives);
            InstallSizeText = "Installation size : " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfInstalledFiles);
            VersionText = "Modlist version : " + Metadata.Version;
            IsBroken = metadata.ValidationSummary.HasFailures || metadata.ForceDown;
            //https://www.wabbajack.org/#/modlists/info?machineURL=eldersouls
            OpenWebsiteCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/#/modlists/info?machineURL={Metadata.NamespacedName}")));

            IsLoadingIdle = new Subject<bool>();
            
            ModListContentsCommend = ReactiveCommand.Create(async () =>
            {
                UIUtils.OpenWebsite(new Uri("https://www.wabbajack.org/search/" + Metadata.NamespacedName));
            }, IsLoadingIdle.StartWith(true));
            
            ExecuteCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _maintainer.HaveModList(Metadata))
                {
                    LoadModlistForInstalling.Send(_maintainer.ModListPath(Metadata), Metadata);
                    NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Installer);
                }
                else
                {
                    await Download();
                }
            }, LoadingLock.WhenAnyValue(ll => ll.IsLoading).Select(v => !v));

            _Exists = Observable.Interval(TimeSpan.FromSeconds(0.5))
                .Unit()
                .StartWith(Unit.Default)
                .FlowSwitch(_parent.WhenAny(x => x.IsActive))
                .SelectAsync(async _ =>
                {
                    try
                    {
                        return !IsDownloading && await maintainer.HaveModList(metadata);
                    }
                    catch (Exception)
                    {
                        return true;
                    }
                })
                .ToGuiProperty(this, nameof(Exists));

            var imageObs = Observable.Return(Metadata.Links.ImageUri)
                .DownloadBitmapImage((ex) => _logger.LogError("Error downloading modlist image {Title}", Metadata.Title), LoadingImageLock);

            _Image = imageObs
                .ToGuiProperty(this, nameof(Image));

            _LoadingImage = imageObs
                .Select(x => false)
                .StartWith(true)
                .ToGuiProperty(this, nameof(LoadingImage));
        }



        private async Task Download()
        {
            Status = ModListStatus.Downloading;
            
            using var ll = LoadingLock.WithLoading();
            var (progress, task) = _maintainer.DownloadModlist(Metadata);
            var dispose = progress
                .BindToStrict(this, vm => vm.ProgressPercent);

            await task;

            await _wjClient.SendMetric("downloading", Metadata.Title);
            await UpdateStatus();
            dispose.Dispose();
        }

        private async Task UpdateStatus()
        {
            if (await _maintainer.HaveModList(Metadata))
                Status = ModListStatus.Downloaded;
            else if (LoadingLock.IsLoading)
                Status = ModListStatus.Downloading;
            else
                Status = ModListStatus.NotDownloaded;
        }

        public enum ModListStatus
        {
            NotDownloaded,
            Downloading,
            Downloaded
        }
    }
}
