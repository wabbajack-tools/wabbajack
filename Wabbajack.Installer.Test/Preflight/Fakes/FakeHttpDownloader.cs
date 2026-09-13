#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.Http;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

/// <summary>
///     Sits ahead of the real HTTP downloader and answers from <see cref="FakeDownloadServer" />. Anything
///     the server does not know goes to the real downloader, so tests that fetch real files still work.
///     Deliberately not an <c>IUrlDownloader</c>: at the highest priority it would otherwise claim every URL
///     the dispatcher is asked to parse.
/// </summary>
public sealed class FakeHttpDownloader : ADownloader<Http>
{
    private readonly FakeDownloadServer _server;
    private readonly HttpDownloader _real;

    public FakeHttpDownloader(FakeDownloadServer server, HttpDownloader real)
    {
        _server = server;
        _real = real;
    }

    public override Priority Priority => Priority.Highest;

    public override Task<Hash> Download(Archive archive, Http state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        return _server.Knows(state)
            ? _server.Download(state, destination, job, token)
            : _real.Download(archive, state, destination, job, token);
    }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return _real.IsAllowed(allowList, state);
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        return _real.Resolve(iniData);
    }

    public override Task<bool> Verify(Archive archive, Http archiveState, IJob job, CancellationToken token)
    {
        return Task.FromResult(true);
    }

    public override IEnumerable<string> MetaIni(Archive a, Http state)
    {
        return _real.MetaIni(a, state);
    }
}
