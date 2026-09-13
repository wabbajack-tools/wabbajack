using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.ManualSources;

/// <summary>
///     A downloader for a source Wabbajack cannot fetch from on its own. It still knows how to read and
///     write the source's <c>.meta</c> lines and how to parse its URLs, so compiling, meta writing and
///     list validation keep working, but asking it to download raises
///     <see cref="ManualDownloadRequiredException" /> so the caller can hand the archive to the user.
/// </summary>
public abstract class AManualSourceDownloader<T> : ADownloader<T>
    where T : IDownloadState
{
    /// <summary>
    ///     The priority the downloader this stands in for had. It matters for <c>Resolve</c>: the
    ///     dispatcher asks downloaders in priority order and takes the first state back, and
    ///     <c>HttpDownloader</c> (<see cref="Priority.Low" />) claims any absolute <c>directURL</c>, so a
    ///     stand-in that sorted below it would hand every MediaFire, Mega, GoogleDrive and ModDB link to
    ///     Http instead - changing what a recompiled list records and which allow-list rule applies.
    ///     Every source here was <see cref="Priority.Normal" /> except Manual, which overrides.
    /// </summary>
    public override Priority Priority => Priority.Normal;

    /// <summary>
    ///     Why this source has to be downloaded by hand, phrased for the user.
    /// </summary>
    protected abstract string Reason { get; }

    public override Task<bool> Prepare()
    {
        return Task.FromResult(true);
    }

    public override Task<bool> Verify(Archive archive, T archiveState, IJob job, CancellationToken token)
    {
        return Task.FromResult(true);
    }

    public override Task<Hash> Download(Archive archive, T state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        throw ManualDownloadRequired(archive, state);
    }

    public Task<TResult> DownloadStream<TResult>(Archive archive, Func<Stream, Task<TResult>> fn,
        CancellationToken token)
    {
        throw ManualDownloadRequired(archive, archive.State);
    }

    protected ManualDownloadRequiredException ManualDownloadRequired(Archive archive, IDownloadState state)
    {
        return new ManualDownloadRequiredException(archive,
            ManualDownloadUrls.TryGet(state, out var target) ? target : null, Reason);
    }
}
