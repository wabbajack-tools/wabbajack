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
        if (_isFinished) return;
        _isFinished = true;
        Resource.Finish(this);
    }

    public long Current { get; internal set; }
    public long? Size { get; set; }

    public async ValueTask Report(long processedSize, CancellationToken token)
    {
        await Resource.Report(this, (int)Math.Min(processedSize, int.MaxValue), token);
        Current += processedSize;
        OnUpdate?.Invoke(this, (Percent.FactoryPutInRange(Current, Size ?? 1), Current));
    }

    public void ReportNoWait(int processedSize)
    {
        Resource.ReportNoWait(this, processedSize);
        OnUpdate?.Invoke(this, (Percent.FactoryPutInRange(Current, Size ?? 1), Current));
    }

    public void ResetProgress()
    {
        Current = 0;
        OnUpdate?.Invoke(this, (Percent.FactoryPutInRange(Current, Size ?? 1), Current));
    }

    public event EventHandler<(Percent Progress, long Processed)> OnUpdate;
}