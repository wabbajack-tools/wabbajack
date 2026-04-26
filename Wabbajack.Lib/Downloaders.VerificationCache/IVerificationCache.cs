using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.VerificationCache;

public interface IVerificationCache
{
    Task<(bool? IsValid, IDownloadState State)> Get(IDownloadState archive);
    Task Put(IDownloadState archive, bool valid);
}