using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Services;

public class ManualDownloader : ADownloader<Manual>
{
    public override Priority Priority { get; }

    public override Task<Hash> Download(Archive archive, Manual state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        var manual = (Manual) state;
        return allowList.AllowedPrefixes.Any(p => manual.Url.ToString().StartsWith(p));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.TryGetValue("manualURL", out var manual)) return new Manual {Url = new Uri(manual)};
        return null;
    }

    public override Task<bool> Verify(Archive archive, Manual archiveState, IJob job, CancellationToken token)
    {
        return Task.FromResult(true);
    }

    public override IEnumerable<string> MetaIni(Archive a, Manual state)
    {
        return new[] {$"manualURL={state.Url}"};
    }
}