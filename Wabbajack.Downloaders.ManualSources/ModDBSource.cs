using System;
using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;

namespace Wabbajack.Downloaders.ManualSources;

public class ModDBSource : AManualSourceDownloader<ModDB>, IProxyable
{
    protected override string Reason => "ModDB files have to be downloaded in a browser";

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") &&
            iniData["directURL"].CleanIniString().StartsWith("https://www.moddb.com/downloads/start") &&
            Uri.TryCreate(iniData["directURL"].CleanIniString().CleanIniString(), UriKind.Absolute, out var uri))
        {
            var state = new ModDB
            {
                Url = uri
            };
            return state;
        }

        return null;
    }

    public IDownloadState? Parse(Uri uri)
    {
        if (!uri.ToString().StartsWith("https://www.moddb.com/downloads/start"))
            return null;
        return new ModDB {Url = uri};
    }

    public Uri UnParse(IDownloadState state)
    {
        return ((ModDB) state).Url;
    }

    public override IEnumerable<string> MetaIni(Archive a, ModDB state)
    {
        return new[] {$"directURL={state.Url}"};
    }
}
