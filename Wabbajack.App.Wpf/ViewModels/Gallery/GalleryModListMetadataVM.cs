using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs;
using Wabbajack.Extensions;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack;

public class GalleryModListMetadataVM : BaseModListMetadataVM
{
    private ModListGalleryVM _parent;

    private readonly ObservableAsPropertyHelper<bool> _Exists;
    public bool Exists => _Exists.Value;

    public GalleryModListMetadataVM(ILogger logger, ModListGalleryVM parent, ModlistMetadata metadata,
        ModListDownloadMaintainer maintainer, Client wjClient, CancellationToken cancellationToken) : base(logger, metadata, maintainer, wjClient, cancellationToken)
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
    }
}
