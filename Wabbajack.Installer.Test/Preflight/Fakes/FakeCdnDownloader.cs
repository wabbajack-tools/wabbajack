#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

/// <summary>
///     Sits ahead of the real CDN downloader. Besides serving registered URLs, it answers the dispatcher's
///     mirror fallback (any URL under the configured mirror server) with "not on the mirror", so a failed
///     download in a test never reaches the network.
/// </summary>
public sealed class FakeCdnDownloader : ADownloader<WabbajackCDN>
{
    private readonly FakeDownloadServer _server;
    private readonly WabbajackCDNDownloader _real;
    private readonly string _mirrorPrefix;

    public FakeCdnDownloader(FakeDownloadServer server, WabbajackCDNDownloader real,
        Wabbajack.Networking.WabbajackClientApi.Configuration configuration)
    {
        _server = server;
        _real = real;
        _mirrorPrefix = configuration.MirrorServerUrl.ToString();
    }

    public override Priority Priority => Priority.Highest;

    public override Task<Hash> Download(Archive archive, WabbajackCDN state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        if (_server.Knows(state))
            return _server.Download(state, destination, job, token);
        if (state.Url.ToString().StartsWith(_mirrorPrefix, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"The fake mirror has nothing for {state.Url}");
        return _real.Download(archive, state, destination, job, token);
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        return _real.Resolve(iniData);
    }

    public override Task<bool> Verify(Archive archive, WabbajackCDN archiveState, IJob job, CancellationToken token)
    {
        return Task.FromResult(true);
    }

    public override IEnumerable<string> MetaIni(Archive a, WabbajackCDN state)
    {
        return _real.MetaIni(a, state);
    }
}
