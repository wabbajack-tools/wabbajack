using System;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.WabbajackClientApi;

public interface IModlistPublisher
{
    Task<(IObservable<(Percent PercentDone, string Message)> Progress, Task PublishTask)>
        PublishModlist(string namespacedName, Version version, AbsolutePath modList,
            DownloadMetadata metadata);
}
