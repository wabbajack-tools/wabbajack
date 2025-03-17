using System;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs;
using Wabbajack.Extensions;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack;

public class GalleryModListMetadataVM : BaseModListMetadataVM
{
    private ModListGalleryVM _parent;

    private readonly ObservableAsPropertyHelper<bool> _Exists;
    public bool Exists => _Exists.Value;
    public ICommand OpenWebsiteCommand { get; }
    public ICommand ModListContentsCommend { get; }

    public GalleryModListMetadataVM(ILogger logger, ModListGalleryVM parent, ModlistMetadata metadata,
        ModListDownloadMaintainer maintainer, ModListSummary? summary, Client wjClient, CancellationToken cancellationToken, HttpClient client, ImageCacheManager icm) : base(logger, metadata, maintainer, summary, wjClient, cancellationToken, client, icm)
    {
        _parent = parent;
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

        OpenWebsiteCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/modlist/{Metadata.NamespacedName}")));

        ModListContentsCommend = ReactiveCommand.Create(async () =>
        {
            UIUtils.OpenWebsite(new Uri($"https://www.wabbajack.org/search/{Metadata.NamespacedName}"));
        }, IsLoadingIdle.StartWith(true));
        

    }
}
