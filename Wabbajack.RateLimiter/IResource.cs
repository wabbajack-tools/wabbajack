using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.RateLimiter
{
    public interface IResource
    {
        StatusReport StatusReport { get; }
        string Name { get; }
        int MaxTasks { get; set; }
        long MaxThroughput { get; set; }
    }
    public interface IResource<T> : IResource
    {
        public ValueTask<Job<T>> Begin(string jobTitle, long size, CancellationToken token);
        ValueTask Report(Job<T> job, int processedSize, CancellationToken token);
        void ReportNoWait(Job<T> job, int processedSize);
        void Finish(Job<T> job);

    }
}