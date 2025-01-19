using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.VerificationCache;

public class NullCache : IVerificationCache
{
    public async Task<(bool? IsValid, IDownloadState State)> Get(IDownloadState state)
    {
        return (null, state);
    }

    public Task Put(IDownloadState archive, bool valid)
    {
        return Task.CompletedTask;
    }
}