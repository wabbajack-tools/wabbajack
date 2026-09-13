#nullable enable
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
///     Sits ahead of the real Nexus downloader so a premium-account test can see the archive reach the
///     dispatcher without an API key or a network.
/// </summary>
public sealed class FakeNexusDownloader : ADownloader<Nexus>
{
    private readonly FakeDownloadServer _server;
    private readonly NexusDownloader _real;

    public FakeNexusDownloader(FakeDownloadServer server, NexusDownloader real)
    {
        _server = server;
        _real = real;
    }

    public override Priority Priority => Priority.Highest;

    /// <summary>
    ///     What <see cref="Prepare" /> answers. The real downloader returns false when no Nexus token is
    ///     stored, which is how an account the login probe called premium still cannot download a thing.
    ///     Each <see cref="PreflightTestHost" /> owns the instance its dispatcher uses, so clearing this
    ///     reaches no other test.
    /// </summary>
    public bool CanPrepare { get; set; } = true;

    public override Task<Hash> Download(Archive archive, Nexus state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        return _server.Knows(state)
            ? _server.Download(state, destination, job, token)
            : _real.Download(archive, state, destination, job, token);
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(CanPrepare);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        return _real.Resolve(iniData);
    }

    public override Task<bool> Verify(Archive archive, Nexus archiveState, IJob job, CancellationToken token)
    {
        return Task.FromResult(true);
    }

    public override IEnumerable<string> MetaIni(Archive a, Nexus state)
    {
        return _real.MetaIni(a, state);
    }
}
