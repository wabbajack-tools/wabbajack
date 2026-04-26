using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Http.Interfaces;

public interface IHttpDownloader
{
    public Task<Hash> Download(HttpRequestMessage message, AbsolutePath dest, IJob job, CancellationToken token);
}