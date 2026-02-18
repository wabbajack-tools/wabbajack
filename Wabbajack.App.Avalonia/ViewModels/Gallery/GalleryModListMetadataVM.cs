using System;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.DTOs;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

public class GalleryModListMetadataVM : BaseModListMetadataVM
{
    private readonly ModListGalleryVM _parent;

    private readonly ObservableAsPropertyHelper<bool> _Exists;
    public bool Exists => _Exists.Value;

    public ICommand OpenWebsiteCommand { get; }
    public ICommand ModListContentsCommand { get; }

    public GalleryModListMetadataVM(
        ILogger logger,
        ModListGalleryVM parent,
        ModlistMetadata metadata,
        ModListDownloadMaintainer maintainer,
        ModListSummary? summary,
        Client wjClient,
        CancellationToken cancellationToken,
        HttpClient client,
        ImageCacheManager icm)
        : base(logger, metadata, maintainer, summary, wjClient, cancellationToken, client, icm)
    {
        _parent = parent;

        _Exists = Observable.Interval(TimeSpan.FromSeconds(0.5))
            .Select(_ => Unit.Default)
            .StartWith(Unit.Default)
            .SelectMany(async _ =>
            {
                try { return !IsDownloading && await maintainer.HaveModList(metadata); }
                catch { return true; }
            })
            .ToProperty(this, nameof(Exists), scheduler: RxApp.MainThreadScheduler);

        OpenWebsiteCommand = ReactiveCommand.Create(() =>
            UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/modlist/{Metadata.NamespacedName}")));

        ModListContentsCommand = ReactiveCommand.Create(() =>
            UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/search/{Metadata.NamespacedName}")),
            IsLoadingIdle.StartWith(true));
    }
}
