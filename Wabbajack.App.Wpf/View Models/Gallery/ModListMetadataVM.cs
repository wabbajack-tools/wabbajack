using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
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
        public string TotalSizeRequirementText { get; private set; }
        
        [Reactive]
        public string VersionText { get; private set; }

        [Reactive]
        public bool ImageContainsTitle { get; private set; }

        [Reactive]

        public bool DisplayVersionOnlyInInstallerView { get; private set; }

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
        private readonly CancellationToken _cancellationToken;

        public ModListMetadataVM(ILogger logger, ModListGalleryVM parent, ModlistMetadata metadata,
            ModListDownloadMaintainer maintainer, ModListSummary[] modlistSummaries, Client wjClient, CancellationToken cancellationToken)
        {
            _logger = logger;
            _parent = parent;
            _maintainer = maintainer;
            Metadata = metadata;
            _wjClient = wjClient;
            _cancellationToken = cancellationToken;
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
            TotalSizeRequirementText =  "Total size requirement: " + UIUtils.FormatBytes(
                    Metadata.DownloadMetadata.SizeOfArchives + Metadata.DownloadMetadata.SizeOfInstalledFiles
                );
            VersionText = "Modlist version : " + Metadata.Version;
            ImageContainsTitle = Metadata.ImageContainsTitle;
            DisplayVersionOnlyInInstallerView = Metadata.DisplayVersionOnlyInInstallerView;
            var modListSummary = GetModListSummaryForModlist(modlistSummaries, metadata.NamespacedName);
            IsBroken = (modListSummary?.HasFailures ?? false) || metadata.ForceDown;
            // https://www.wabbajack.org/modlist/wj-featured/aldrnari
            OpenWebsiteCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/modlist/{Metadata.NamespacedName}")));

            IsLoadingIdle = new Subject<bool>();
            
            ModListContentsCommend = ReactiveCommand.Create(async () =>
            {
                UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/search/{Metadata.NamespacedName}"));
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
            }, LoadingLock.WhenAnyValue(ll => ll.IsLoading)
                .CombineLatest(this.WhenAnyValue(vm => vm.IsBroken))
                .Select(v => !v.First && !v.Second));

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
            try
            {
                Status = ModListStatus.Downloading;

                using var ll = LoadingLock.WithLoading();
                var (progress, task) = _maintainer.DownloadModlist(Metadata, _cancellationToken);
                var dispose = progress
                    .BindToStrict(this, vm => vm.ProgressPercent);
                try
                {
                    await _wjClient.SendMetric("downloading", Metadata.Title);
                    await task;
                    await UpdateStatus();
                }
                finally
                {
                    dispose.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While downloading {Modlist}", Metadata.RepositoryName);
                await UpdateStatus();
            }
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

        private static ModListSummary GetModListSummaryForModlist(ModListSummary[] modListSummaries, string machineUrl)
        {
            return modListSummaries.FirstOrDefault(x => x.MachineURL == machineUrl);
        }
    }
}
