using System;
using System.Collections.Generic;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;

namespace Wabbajack.Downloaders.ManualSources;

/// <summary>
///     Metadata handling for an Invision Power Services site. <paramref name="siteURL" /> is the same
///     value the API-driven downloader used, so a resolved state is identical to what it produced.
/// </summary>
public abstract class AIPS4Source<TState> : AManualSourceDownloader<TState>
    where TState : IPS4OAuth2, new()
{
    private readonly string _siteName;
    private readonly Uri _siteURL;

    protected AIPS4Source(Uri siteURL, string siteName)
    {
        _siteURL = siteURL;
        _siteName = siteName;
    }

    protected override string Reason => $"{_siteName} files have to be downloaded in a browser";

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (!iniData.ContainsKey("ips4Site") || iniData["ips4Site"] != _siteName) return null;

        if (iniData.ContainsKey("ips4Mod") && iniData.ContainsKey("ips4File"))
        {
            if (!long.TryParse(iniData["ips4Mod"], out var parsedMod))
                return null;
            var state = new TState {IPS4Mod = parsedMod, IPS4File = iniData["ips4File"]};
            return state;
        }

        if (iniData.ContainsKey("ips4Attachment") != default)
        {
            if (!long.TryParse(iniData["ips4Attachment"], out var parsedMod))
                return null;
            var state = new TState
            {
                IPS4Mod = parsedMod,
                IsAttachment = true,
                IPS4Url = $"{_siteURL}/applications/core/interface/file/attachment.php?id={parsedMod}"
            };

            return state;
        }

        return null;
    }

    public override IEnumerable<string> MetaIni(Archive a, TState state)
    {
        return new[]
        {
            $"ips4Site={_siteName}",
            $"ips4Mod={state.IPS4Mod}",
            $"ips4File={state.IPS4File}"
        };
    }
}
