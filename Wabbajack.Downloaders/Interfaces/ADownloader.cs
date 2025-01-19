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

public abstract class ADownloader<T> : IDownloader<T>
    where T : IDownloadState
{
    public abstract Task<Hash> Download(Archive archive, T state, AbsolutePath destination,
        IJob job, CancellationToken token);

    public bool CanDownload(Archive a)
    {
        return a.State is T;
    }

    public Task<Hash> Download(Archive archive, AbsolutePath destination, IJob job, CancellationToken token)
    {
        return Download(archive, (T) archive.State, destination, job, token);
    }

    public Task<bool> Verify(Archive archive, IJob job, CancellationToken token)
    {
        return Verify(archive, (T) archive.State, job, token);
    }

    public abstract Task<bool> Prepare();
    public abstract bool IsAllowed(ServerAllowList allowList, IDownloadState state);

    public IEnumerable<string> MetaIni(Archive a)
    {
        return MetaIni(a, (T) a.State);
    }

    public abstract IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData);
    public abstract Priority Priority { get; }

    public abstract Task<bool> Verify(Archive archive, T archiveState, IJob job, CancellationToken token);
    public abstract IEnumerable<string> MetaIni(Archive a, T state);
}