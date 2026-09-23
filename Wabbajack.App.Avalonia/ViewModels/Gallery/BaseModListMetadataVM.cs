using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Models;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

public readonly record struct ModListTag(string name)
{
    public string Name { get; } = name;
    public override string ToString() => Name;
}

public readonly record struct ModListMod(string name)
{
    public string Name { get; } = name;
    public override string ToString() => Name;
}

/// <summary>A modlist in the gallery or the installer: its metadata, image, sizes, and whether it can be installed.</summary>
public partial class BaseModListMetadataVM : ViewModel
{
    protected readonly ILogger _logger;
    protected readonly ModListDownloadMaintainer _maintainer;
    protected readonly Client _wjClient;
    protected readonly CancellationToken _cancellationToken;
    protected readonly Navigator _navigator;
    protected readonly Subject<bool> IsLoadingIdle = new();

    private readonly ObservableAsPropertyHelper<Bitmap?> _image;
    private readonly ObservableAsPropertyHelper<bool> _loadingImage;
    private readonly ObservableAsPropertyHelper<Bitmap?> _gameIcon;

    public BaseModListMetadataVM(ILogger logger, ModlistMetadata metadata, ModListDownloadMaintainer maintainer,
        ModListSummary? summary, Client wjClient, CancellationToken cancellationToken, HttpClient client,
        ImageCacheManager icm, GameIconCache gameIcons, Navigator navigator)
    {
        _logger = logger;
        _maintainer = maintainer;
        _wjClient = wjClient;
        _cancellationToken = cancellationToken;
        _navigator = navigator;
        Metadata = metadata;
        Summary = summary;

        GameMetaData = Metadata.Game.MetaData();
        Location = AppFolders.CommonFolder.Value.Combine("downloaded_mod_lists", Metadata.NamespacedName)
            .WithExtension(Ext.Wabbajack);

        UpdateStatus().FireAndForget();

        ModListTagList = Metadata.Tags?.Select(tag => new ModListTag(tag)).ToHashSet() ?? new HashSet<ModListTag>();
        ModListTagList.Add(new ModListTag(GameMetaData.HumanFriendlyGameName));

        // Every gallery entry carries download metadata; the WPF app relied on it the same way.
        var sizes = Metadata.DownloadMetadata!;
        DownloadSizeText = "Download size: " + UIUtils.FormatBytes(sizes.SizeOfArchives);
        InstallSizeText = "Installation size: " + UIUtils.FormatBytes(sizes.SizeOfInstalledFiles);
        TotalSizeRequirementText = "Total size requirement: " + UIUtils.FormatBytes(sizes.TotalSize);
        VersionText = "v" + Metadata.Version;
        ImageContainsTitle = Metadata.ImageContainsTitle;
        DisplayVersionOnlyInInstallerView = Metadata.DisplayVersionOnlyInInstallerView;
        IsBroken = (Summary?.HasFailures ?? false) || metadata.ForceDown;

        var imageUri = UIUtils.GetLargeImageUri(metadata);
        _image = Observable.Return(imageUri)
            .DownloadBitmap(ex => _logger.LogError("Error downloading modlist image {Title} from {ImageUri}: {Exception}",
                Metadata.Title, imageUri, ex.ToString()), LoadingImageLock, client, icm)
            .ToProperty(this, nameof(Image), deferSubscription: true, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(CompositeDisposable);

        _loadingImage = LoadingImageLock.WhenAnyValue(x => x.IsLoading)
            .ToProperty(this, nameof(LoadingImage), scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(CompositeDisposable);

        _gameIcon = gameIcons.Get(GameMetaData.IconSource)
            .ToProperty(this, nameof(GameIcon), scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(CompositeDisposable);

        InstallCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!await _maintainer.HaveModList(Metadata))
                    await Download();
                Install();
            }, LoadingLock.WhenAnyValue(ll => ll.IsLoading)
                .CombineLatest(this.WhenAnyValue(vm => vm.IsBroken))
                .Select(v => !v.First && !v.Second));

        DetailsCommand = ReactiveCommand.Create(() =>
        {
            LoadModlistForDetails.Send(this);
            ShowFloatingWindow.Send(FloatingScreenType.ModListDetails);
        });
    }

    public ModlistMetadata Metadata { get; }
    public AbsolutePath Location { get; }
    public LoadingLock LoadingImageLock { get; } = new();
    public ModListSummary? Summary { get; }

    [Reactive] public partial HashSet<ModListTag> ModListTagList { get; protected set; }
    [Reactive] public partial Percent ProgressPercent { get; set; }
    [Reactive] public partial bool IsBroken { get; protected set; }
    [Reactive] public partial ModListStatus Status { get; set; }
    [Reactive] public partial bool IsDownloading { get; protected set; }
    [Reactive] public partial string DownloadSizeText { get; protected set; }
    [Reactive] public partial string InstallSizeText { get; protected set; }
    [Reactive] public partial string TotalSizeRequirementText { get; protected set; }
    [Reactive] public partial string VersionText { get; set; }
    [Reactive] public partial bool ImageContainsTitle { get; protected set; }
    [Reactive] public partial GameMetaData GameMetaData { get; protected set; }
    [Reactive] public partial bool DisplayVersionOnlyInInstallerView { get; protected set; }

    public ICommand DetailsCommand { get; }
    public ICommand InstallCommand { get; }

    public Bitmap? Image => _image.Value;
    public bool LoadingImage => _loadingImage.Value;
    public Bitmap? GameIcon => _gameIcon.Value;

    private void Install()
    {
        LoadModlistForInstalling.Send(_maintainer.ModListPath(Metadata), Metadata);
        _navigator.NavigateTo(ScreenType.Installer);
        ShowFloatingWindow.Send(FloatingScreenType.None);
    }

    protected async Task Download()
    {
        try
        {
            Status = ModListStatus.Downloading;

            using var ll = LoadingLock.WithLoading();
            var (progress, task) = _maintainer.DownloadModlist(Metadata, _cancellationToken);
            using var _ = progress.ObserveOn(RxApp.MainThreadScheduler).Subscribe(p => ProgressPercent = p);
            await _wjClient.SendMetric("downloading", Metadata.Title);
            await task;
            await UpdateStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While downloading {Modlist}", Metadata.RepositoryName);
            await UpdateStatus();
        }
    }

    /// <summary>
    /// Re-reads whether the list is downloaded. The answer is applied on the UI thread: these view models are
    /// built on a background task, and Avalonia refuses a bound property changed anywhere else.
    /// </summary>
    protected async Task UpdateStatus()
    {
        var status = await _maintainer.HaveModList(Metadata)
            ? ModListStatus.Downloaded
            : LoadingLock.IsLoading ? ModListStatus.Downloading : ModListStatus.NotDownloaded;
        RxApp.MainThreadScheduler.Schedule(() => Status = status);
    }

    public enum ModListStatus
    {
        NotDownloaded,
        Downloading,
        Downloaded
    }
}

/// <summary>Asks the details pane to show a modlist.</summary>
public class LoadModlistForDetails(BaseModListMetadataVM modlist)
{
    public BaseModListMetadataVM MetadataVM { get; } = modlist;

    public static void Send(BaseModListMetadataVM modlist) => MessageBus.Current.SendMessage(new LoadModlistForDetails(modlist));
}
