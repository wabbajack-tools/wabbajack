using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack;


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

public partial class BaseModListMetadataVM : ViewModel
{
    public ModlistMetadata Metadata { get; }
    public AbsolutePath Location { get; }
    public LoadingLock LoadingImageLock { get; } = new();
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

    [Reactive] public partial ICommand DetailsCommand { get; set; }
    [Reactive] public partial ICommand InstallCommand { get; protected set; }

    [Reactive] public partial IValidationResult Error { get; protected set; }

    // TODO(avalonia): UIUtils.DownloadBitmapImage still returns System.Windows.Media.Imaging.BitmapImage
    // (WPF) until Wabbajack.App.Avalonia/Util/UIUtils.cs is converted to Avalonia.Media.Imaging.Bitmap.
    protected ObservableAsPropertyHelper<Bitmap> _Image { get; set; }
    public Bitmap Image => _Image.Value;

    protected ObservableAsPropertyHelper<bool> _LoadingImage { get; set; }
    public bool LoadingImage => _LoadingImage.Value;

    // GameMetaData.IconSource is a remote URL string. WPF's ImageSourceConverter fetched those
    // implicitly, so the XAML could bind ImageBrush.Source straight to it; Avalonia's Source takes an
    // IImage and does no fetching, which left the game badge on every gallery tile empty. Downloaded
    // through the same cache the modlist images use.
    protected ObservableAsPropertyHelper<Bitmap> _GameIcon { get; set; }
    public Bitmap GameIcon => _GameIcon?.Value;

    public ModListSummary? Summary { get; set; }

    protected Subject<bool> IsLoadingIdle;
    protected readonly ILogger _logger;
    protected readonly ModListDownloadMaintainer _maintainer;
    protected readonly Client _wjClient;
    protected readonly CancellationToken _cancellationToken;
    protected readonly ServiceProvider _serviceProvider;
    protected readonly ImageCacheManager _icm;

    public BaseModListMetadataVM(ILogger logger, ModlistMetadata metadata,
        ModListDownloadMaintainer maintainer, ModListSummary? summary, Client wjClient, CancellationToken cancellationToken, HttpClient client, ImageCacheManager icm)
    {
        _logger = logger;
        _maintainer = maintainer;
        Metadata = metadata;
        Summary = summary;
        _wjClient = wjClient;
        _cancellationToken = cancellationToken;

        GameMetaData = Metadata.Game.MetaData();
        Location = LauncherUpdater.CommonFolder.Value.Combine("downloaded_mod_lists", Metadata.NamespacedName).WithExtension(Ext.Wabbajack);
        
        UpdateStatus().FireAndForget();

        ModListTagList = Metadata.Tags?.Select(tag => new ModListTag(tag)).ToHashSet();
        ModListTagList.Add(new ModListTag(GameMetaData.HumanFriendlyGameName));

        DownloadSizeText = "Download size: " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfArchives);
        InstallSizeText = "Installation size: " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfInstalledFiles);
        TotalSizeRequirementText =  "Total size requirement: " + UIUtils.FormatBytes( Metadata.DownloadMetadata.TotalSize );
        VersionText = "v" + Metadata.Version;
        ImageContainsTitle = Metadata.ImageContainsTitle;
        DisplayVersionOnlyInInstallerView = Metadata.DisplayVersionOnlyInInstallerView;
        IsBroken = (Summary?.HasFailures ?? false) || metadata.ForceDown;

        IsLoadingIdle = new Subject<bool>();

        var smallImageUri = UIUtils.GetLargeImageUri(metadata);
        var imageObs = Observable.Return(smallImageUri)
            .DownloadBitmapImage(
                (ex) => _logger.LogError("Error downloading modlist image {Title} from {ImageUri}: {Exception}",
                    Metadata.Title, smallImageUri, ex.ToString()), LoadingImageLock, client, icm);

            _Image = imageObs
                .ToGuiProperty(this, nameof(Image))
                .DisposeWith(CompositeDisposable);

            _LoadingImage = imageObs
                .Select(x => false)
                .StartWith(true)
                .ToGuiProperty(this, nameof(LoadingImage))
                .DisposeWith(CompositeDisposable);

        // Only absolute URLs: GameMetaData falls back to a relative path ("Resources/Icons/...")
        // for games with no icon, which is not something the downloader can fetch.
        var iconSource = GameMetaData?.IconSource;
        if (!string.IsNullOrWhiteSpace(iconSource) &&
            Uri.TryCreate(iconSource, UriKind.Absolute, out var iconUri) &&
            (iconUri.Scheme == Uri.UriSchemeHttp || iconUri.Scheme == Uri.UriSchemeHttps))
        {
            _GameIcon = Observable.Return(iconSource)
                .DownloadBitmapImage(
                    ex => _logger.LogError("Error downloading game icon for {Game} from {IconUri}: {Exception}",
                        GameMetaData.HumanFriendlyGameName, iconSource, ex.ToString()),
                    LoadingImageLock, client, icm)
                .ToGuiProperty(this, nameof(GameIcon))
                .DisposeWith(CompositeDisposable);
        }

        InstallCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (await _maintainer.HaveModList(Metadata))
            {
                Install();
            }
            else
            {
                await Download();
                Install();
            }
        }, LoadingLock.WhenAnyValue(ll => ll.IsLoading)
            .CombineLatest(this.WhenAnyValue(vm => vm.IsBroken))
            .Select(v => !v.First && !v.Second));

        DetailsCommand = ReactiveCommand.Create(() => {
            LoadModlistForDetails.Send(this);
            ShowFloatingWindow.Send(FloatingScreenType.ModListDetails);
        });
    }

    private void Install()
    {
        LoadModlistForInstalling.Send(_maintainer.ModListPath(Metadata), Metadata);
        NavigateToGlobal.Send(ScreenType.Installer);
        ShowFloatingWindow.Send(FloatingScreenType.None);
    }

    protected async Task Download()
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

    protected async Task UpdateStatus()
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
