using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;

namespace Wabbajack.Downloaders.ManualSources;

public class MediaFireSource : AManualSourceDownloader<MediaFire>, IUrlDownloader, IProxyable
{
    protected override string Reason => "MediaFire files have to be downloaded in a browser";

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        var mediaFireState = (MediaFire) state;
        return allowList.AllowedPrefixes.Any(p => mediaFireState.Url.ToString().StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") &&
            Uri.TryCreate(iniData["directURL"].CleanIniString(), UriKind.Absolute, out var uri)
            && uri.Host == "www.mediafire.com")
        {
            var state = new MediaFire
            {
                Url = uri
            };
            return state;
        }

        return null;
    }

    public IDownloadState? Parse(Uri uri)
    {
        if (uri.Host != "www.mediafire.com")
            return null;
        return new MediaFire {Url = uri};
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((MediaFire) state).Url;
    }

    public override IEnumerable<string> MetaIni(Archive a, MediaFire state)
    {
        return new[] {$"directURL={state.Url}"};
    }
}
