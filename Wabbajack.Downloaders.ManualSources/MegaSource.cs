using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;

namespace Wabbajack.Downloaders.ManualSources;

public class MegaSource : AManualSourceDownloader<Mega>, IUrlDownloader, IProxyable
{
    private const string MegaPrefix = "https://mega.nz/#!";
    private const string MegaFilePrefix = "https://mega.nz/file/";

    protected override string Reason => "Mega files have to be downloaded in a browser";

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        var megaState = (Mega) state;
        return allowList.AllowedPrefixes.Any(p => megaState.Url.ToString().StartsWith(p));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        return iniData.ContainsKey("directURL") ? GetDownloaderState(iniData["directURL"].CleanIniString()) : null;
    }

    public IDownloadState? Parse(Uri uri)
    {
        return GetDownloaderState(uri.ToString());
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((Mega) state).Url;
    }

    private Mega? GetDownloaderState(string? url)
    {
        if (url == null) return null;

        if (url.StartsWith(MegaPrefix) || url.StartsWith(MegaFilePrefix))
            return new Mega {Url = new Uri(url)};
        return null;
    }

    public override IEnumerable<string> MetaIni(Archive a, Mega state)
    {
        return new[] {$"directURL={state.Url}"};
    }
}
