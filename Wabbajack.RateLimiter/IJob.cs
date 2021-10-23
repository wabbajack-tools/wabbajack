using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.RateLimiter;

public interface IJob
{
    public long? Size { get; set; }
    public long Current { get; }
    public ValueTask Report(int processedSize, CancellationToken token);
}