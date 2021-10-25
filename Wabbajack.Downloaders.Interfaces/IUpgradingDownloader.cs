using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.Interfaces;

public interface IUpgradingDownloader
{
    public Task<Archive?> TryGetUpgrade(Archive archive, IJob job, TemporaryFileManager temporaryFileManager,
        CancellationToken token);
}