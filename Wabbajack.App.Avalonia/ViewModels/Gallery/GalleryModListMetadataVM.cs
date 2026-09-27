using System;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.DTOs;
using Wabbajack.Extensions;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

/// <summary>A gallery tile's modlist. While the gallery is on screen it checks twice a second whether the list is downloaded.</summary>
public class GalleryModListMetadataVM : BaseModListMetadataVM
{
    private readonly ObservableAsPropertyHelper<bool> _exists;

    public GalleryModListMetadataVM(ILogger logger, ModListGalleryVM parent, ModlistMetadata metadata,
        ModListDownloadMaintainer maintainer, ModListSummary? summary, Client wjClient, CancellationToken cancellationToken,
        HttpClient client, ImageCacheManager icm, GameIconCache gameIcons, Navigator navigator)
        : base(logger, metadata, maintainer, summary, wjClient, cancellationToken, client, icm, gameIcons, navigator)
    {
        _exists = Observable.Interval(TimeSpan.FromSeconds(0.5))
            .Unit()
            .StartWith(Unit.Default)
            .FlowSwitch(parent.WhenAny(x => x.IsActive, x => x.Value))
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

        OpenWebsiteCommand = ReactiveCommand.Create(() =>
            UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/modlist/{Metadata.NamespacedName}")));

        ModListContentsCommend = ReactiveCommand.Create(
            () => UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/search/{Metadata.NamespacedName}")),
            IsLoadingIdle.StartWith(true));
    }

    public bool Exists => _exists.Value;
    public ICommand OpenWebsiteCommand { get; }
    public ICommand ModListContentsCommend { get; }
}
