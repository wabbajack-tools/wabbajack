using System;
using System.Collections.Generic;
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
using Wabbajack.Lib;
using Wabbajack.Lib.Extensions;
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

        [Reactive]
        public List<ModListTag> ModListTagList { get; private set; }

        [Reactive]
        public Percent ProgressPercent { get; private set; }

        [Reactive]
        public bool IsBroken { get; private set; }
        
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
        private readonly ILogger<ModListMetadataVM> _logger;
        private readonly ModListDownloadMaintainer _maintainer;
        private readonly Client _wjClient;

        public ModListMetadataVM(ILogger<ModListMetadataVM> logger, ModListGalleryVM parent, ModlistMetadata metadata,
            ModListDownloadMaintainer maintainer, Client wjClient)
        {
            _logger = logger;
            _parent = parent;
            _maintainer = maintainer;
            Metadata = metadata;
            _wjClient = wjClient;
            Location = LauncherUpdater.CommonFolder.Value.Combine("downloaded_mod_lists", Metadata.Links.MachineURL).WithExtension(Ext.Wabbajack);
            ModListTagList = new List<ModListTag>();

            Metadata.tags.ForEach(tag =>
            {
                ModListTagList.Add(new ModListTag(tag));
            });
            ModListTagList.Add(new ModListTag(metadata.Game.MetaData().HumanFriendlyGameName));

            DownloadSizeText = "Download size : " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfArchives);
            InstallSizeText = "Installation size : " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfInstalledFiles);
            VersionText = "Modlist version : " + Metadata.Version;
            IsBroken = metadata.ValidationSummary.HasFailures || metadata.ForceDown;
            //https://www.wabbajack.org/#/modlists/info?machineURL=eldersouls
            OpenWebsiteCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/#/modlists/info?machineURL={Metadata.Links.MachineURL}")));

            IsLoadingIdle = new Subject<bool>();
            
            ModListContentsCommend = ReactiveCommand.Create(async () =>
            {
                _parent.MWVM.ModListContentsVM.Value.Name = metadata.Title;
                IsLoadingIdle.OnNext(false);
                try
                {
                    var status = await ClientAPIEx.GetDetailedStatus(metadata.Links.MachineURL);
                    var coll = _parent.MWVM.ModListContentsVM.Value.Status;
                    coll.Clear();
                    coll.AddRange(status.Archives);
                    _parent.MWVM.NavigateTo(_parent.MWVM.ModListContentsVM.Value);
                }
                finally
                {
                    IsLoadingIdle.OnNext(true);
                }
            }, IsLoadingIdle.StartWith(true));
            ExecuteCommand = ReactiveCommand.CreateFromObservable<Unit, Unit>(
                canExecute: this.WhenAny(x => x.IsBroken).Select(x => !x),
                execute: (unit) =>
                Observable.Return(unit)
                .WithLatestFrom(
                    this.WhenAny(x => x.Exists),
                    (_, e) => e)
                // Do any download work on background thread
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectTask(async (exists) =>
                {
                    if (!exists)
                    {
                        try
                        {
                            await Download();
                        }
                        catch (Exception ex)
                        {
                            Error = ErrorResponse.Fail(ex);
                            return false;
                        }
                        // Return an updated check on exists
                        return Location.FileExists();
                    }
                    return exists;
                })
                .Where(exists => exists)
                // Do any install page swap over on GUI thread
                .ObserveOnGuiThread()
                .Select(_ =>
                {
                    _parent.MWVM.OpenInstaller(Location);

                    // Wait for modlist member to be filled, then open its readme
                    return _parent.MWVM.Installer.Value.WhenAny(x => x.ModList)
                        .NotNull()
                        .Take(1)
                        .Do(modList =>
                        {
                            try
                            {
                                modList.OpenReadme();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "While opening modlist README");
                            }
                        });
                })
                .Switch()
                .Unit());

            _Exists = Observable.Interval(TimeSpan.FromSeconds(0.5))
                .Unit()
                .StartWith(Unit.Default)
                .FlowSwitch(_parent.WhenAny(x => x.IsActive))
                .SelectAsync(async _ =>
                {
                    try
                    {
                        return !IsDownloading && !(await maintainer.HaveModList(metadata));
                    }
                    catch (Exception)
                    {
                        return true;
                    }
                })
                .ToGuiProperty(this, nameof(Exists));

            var imageObs = Observable.Return(Metadata.Links.ImageUri)
                .DownloadBitmapImage((ex) => _logger.LogError("Error downloading modlist image {Title}", Metadata.Title));

            _Image = imageObs
                .ToGuiProperty(this, nameof(Image));

            _LoadingImage = imageObs
                .Select(x => false)
                .StartWith(true)
                .ToGuiProperty(this, nameof(LoadingImage));
        }



        private async Task Download()
        {
            var (progress, task) = _maintainer.DownloadModlist(Metadata);
            var dispose = progress.Subscribe(p => ProgressPercent = p);

            await task;

            await _wjClient.SendMetric("downloading", Metadata.Title);
            dispose.Dispose();
        }
    }
}
