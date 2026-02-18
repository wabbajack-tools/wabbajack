using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Models;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.ModListValidation;
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

public class BaseModListMetadataVM : ViewModelBase
{
    public ModlistMetadata Metadata { get; }
    public AbsolutePath Location { get; }
    public LoadingLock LoadingImageLock { get; } = new();

    [Reactive] public HashSet<ModListTag> ModListTagList { get; protected set; } = new();
    [Reactive] public Percent ProgressPercent { get; set; }
    [Reactive] public bool IsBroken { get; protected set; }
    [Reactive] public ModListStatus Status { get; set; }
    [Reactive] public bool IsDownloading { get; protected set; }
    [Reactive] public string DownloadSizeText { get; protected set; } = string.Empty;
    [Reactive] public string InstallSizeText { get; protected set; } = string.Empty;
    [Reactive] public string TotalSizeRequirementText { get; protected set; } = string.Empty;
    [Reactive] public string VersionText { get; set; } = string.Empty;
    [Reactive] public bool ImageContainsTitle { get; protected set; }
    [Reactive] public GameMetaData GameMetaData { get; protected set; } = null!;
    [Reactive] public bool DisplayVersionOnlyInInstallerView { get; protected set; }

    [Reactive] public ICommand? DetailsCommand { get; set; }
    [Reactive] public ICommand? InstallCommand { get; protected set; }

    protected ObservableAsPropertyHelper<Bitmap?> _Image { get; set; } = null!;
    public Bitmap? Image => _Image.Value;

    protected ObservableAsPropertyHelper<bool> _LoadingImage { get; set; } = null!;
    public bool LoadingImage => _LoadingImage.Value;

    public ModListSummary? Summary { get; set; }

    protected Subject<bool> IsLoadingIdle = new();
    protected readonly ILogger _logger;
    protected readonly ModListDownloadMaintainer _maintainer;
    protected readonly Client _wjClient;
    protected readonly CancellationToken _cancellationToken;

    public BaseModListMetadataVM(
        ILogger logger,
        ModlistMetadata metadata,
        ModListDownloadMaintainer maintainer,
        ModListSummary? summary,
        Client wjClient,
        CancellationToken cancellationToken,
        HttpClient client,
        ImageCacheManager icm)
    {
        _logger = logger;
        _maintainer = maintainer;
        Metadata = metadata;
        Summary = summary;
        _wjClient = wjClient;
        _cancellationToken = cancellationToken;

        GameMetaData = Metadata.Game.MetaData();
        Location = KnownFolders.WabbajackAppLocal
            .Combine("downloaded_mod_lists", Metadata.NamespacedName)
            .WithExtension(Ext.Wabbajack);

        UpdateStatus().FireAndForget();

        ModListTagList = Metadata.Tags?.Select(tag => new ModListTag(tag)).ToHashSet() ?? new HashSet<ModListTag>();
        ModListTagList.Add(new ModListTag(GameMetaData.HumanFriendlyGameName));

        DownloadSizeText = "Download size: " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfArchives);
        InstallSizeText = "Installation size: " + UIUtils.FormatBytes(Metadata.DownloadMetadata.SizeOfInstalledFiles);
        TotalSizeRequirementText = "Total size requirement: " + UIUtils.FormatBytes(Metadata.DownloadMetadata.TotalSize);
        VersionText = "v" + Metadata.Version;
        ImageContainsTitle = Metadata.ImageContainsTitle;
        DisplayVersionOnlyInInstallerView = Metadata.DisplayVersionOnlyInInstallerView;
        IsBroken = (Summary?.HasFailures ?? false) || metadata.ForceDown;

        var imageUri = UIUtils.GetLargeImageUri(metadata);
        var imageObs = Observable.Return(imageUri)
            .DownloadBitmapImage(
                ex => _logger.LogError("Error downloading modlist image {Title} from {Uri}: {Ex}",
                    Metadata.Title, imageUri, ex.ToString()),
                LoadingImageLock, client, icm);

        _Image = imageObs
            .ToProperty(this, nameof(Image), scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(CompositeDisposable);

        _LoadingImage = imageObs
            .Select(_ => false)
            .StartWith(true)
            .ToProperty(this, nameof(LoadingImage), scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(CompositeDisposable);

        InstallCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _maintainer.HaveModList(Metadata))
                    await Install();
                else
                {
                    await Download();
                    await Install();
                }
            },
            LoadingLock.WhenAnyValue(ll => ll.IsLoading)
                .CombineLatest(this.WhenAnyValue(vm => vm.IsBroken))
                .Select(v => !v.First && !v.Second));
    }

    protected virtual Task Install()
    {
        // Navigation wired up in a later milestone.
        return Task.CompletedTask;
    }

    protected async Task Download()
    {
        try
        {
            Status = ModListStatus.Downloading;
            using var ll = LoadingLock.WithLoading();
            var (progress, task) = _maintainer.DownloadModlist(Metadata, _cancellationToken);
            var dispose = progress.Subscribe(p => ProgressPercent = p);
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
