using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;

namespace Wabbajack.Downloaders.ManualSources;

public class ManualSourceDownloader : AManualSourceDownloader<Manual>, IProxyable
{
    protected override string Reason => "This file has to be downloaded by hand";

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return allowList.AllowedPrefixes.Any(p => ((Manual) state).Url.ToString().StartsWith(p));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("manualURL") && Uri.TryCreate(iniData["manualURL"].CleanIniString(), UriKind.Absolute, out var uri))
        {
            iniData.TryGetValue("prompt", out var prompt);

            var state = new Manual
            {
                Url = uri,
                Prompt = prompt ?? ""
            };

            return state;
        }

        return null;
    }

    public override IEnumerable<string> MetaIni(Archive a, Manual state)
    {
        return new[] { $"manualURL={state.Url}", $"prompt={state.Prompt}" };
    }

    public IDownloadState? Parse(Uri uri)
    {
        return new Manual() { Url = uri };
    }

    public Uri UnParse(IDownloadState state)
    {
        return (state as Manual)!.Url;
    }
}
