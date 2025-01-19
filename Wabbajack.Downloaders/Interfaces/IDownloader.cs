using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.Interfaces;

public interface IDownloader
{
    public Priority Priority { get; }

    /// <summary>
    ///     Returns true if this downloader works with the download state of the given archive
    /// </summary>
    /// <param name="archive"></param>
    /// <returns></returns>
    public bool CanDownload(Archive archive);

    /// <summary>
    ///     Download the given archive to the given path, returning the hashcode of the downloaded data. This
    ///     will never be called on an archive for which `CanDownload` returned false.
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="destination"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<Hash> Download(Archive archive, AbsolutePath destination, IJob job, CancellationToken token);

    /// <summary>
    ///     Return true if the given archive state is still valid.
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<bool> Verify(Archive archive, IJob job, CancellationToken token);

    /// <summary>
    ///     Starts the downloader and configures it to start downloading. Should return null if more data is needed
    ///     before this download can download data
    /// </summary>
    /// <returns></returns>
    public Task<bool> Prepare();

    public bool IsAllowed(ServerAllowList allowList, IDownloadState state);
    IEnumerable<string> MetaIni(Archive a);

    public IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData);
}

public interface IDownloader<T> : IDownloader
    where T : IDownloadState
{
    public Task<Hash> Download(Archive archive, T state, AbsolutePath destination, IJob job, CancellationToken token);
}