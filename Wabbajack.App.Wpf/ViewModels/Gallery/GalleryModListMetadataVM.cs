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
using System.Windows.Media;

namespace Wabbajack
{
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
}
