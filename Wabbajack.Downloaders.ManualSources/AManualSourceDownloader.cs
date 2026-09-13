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
    public override Priority Priority => Priority.Lowest;

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
