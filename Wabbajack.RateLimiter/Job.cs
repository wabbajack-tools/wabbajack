using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.RateLimiter;

public class Job<T> : IJob, IDisposable
{
    public ulong ID { get; internal init; }
    public string Description { get; internal init; }
    public bool Started { get; internal set; }
    public IResource<T> Resource { get; init; }

    private bool _isFinished = false;

    public void Dispose()
    {
        if (!_isFinished) return;
        _isFinished = true;
        Resource.Finish(this);
    }

    public long Current { get; internal set; }
    public long? Size { get; set; }

    public async ValueTask Report(int processedSize, CancellationToken token)
    {
        await Resource.Report(this, processedSize, token);
        Current += processedSize;
    }

    public void ReportNoWait(int processedSize)
    {
        Resource.ReportNoWait(this, processedSize);
    }
}