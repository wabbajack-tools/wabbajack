using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Wabbajack.RateLimiter;

public class Resource<T> : IResource<T>
{
    private Channel<PendingReport> _channel;
    private SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<ulong, Job<T>> _tasks;
    private readonly Task _initialized;
    private ulong _nextId;
    private long _totalUsed;
    public IEnumerable<IJob> Jobs => _tasks.Values;

    public Resource(string? humanName = null, int? maxTasks = null, long maxThroughput = long.MaxValue, CancellationToken? token = null)
    {
        Name = humanName ?? "<unknown>";
        MaxTasks = maxTasks ?? Environment.ProcessorCount;
        MaxThroughput = maxThroughput;
        _semaphore = new SemaphoreSlim(MaxTasks);
        _channel = Channel.CreateBounded<PendingReport>(10);
        _tasks = new ConcurrentDictionary<ulong, Job<T>>();
        _initialized = Task.CompletedTask;

        var tsk = StartTask(token ?? CancellationToken.None);
    }

    /// <summary>
    ///     Settings arrive asynchronously, so the semaphore and channel do not exist yet when the constructor
    ///     returns. <see cref="Begin" /> and <see cref="Report" /> wait on <see cref="_initialized" /> before
    ///     touching either.
    /// </summary>
    public Resource(string humanName, Func<Task<(int MaxTasks, long MaxThroughput)>> settingGetter, CancellationToken? token = null)
    {
        Name = humanName;
        _tasks = new ConcurrentDictionary<ulong, Job<T>>();

        _initialized = Task.Run(async () =>
        {
            var (maxTasks, maxThroughput) = await settingGetter();
            MaxTasks = maxTasks;
            MaxThroughput = maxThroughput;
            _semaphore = new SemaphoreSlim(MaxTasks);
            _channel = Channel.CreateBounded<PendingReport>(10);

            // Not awaited: StartTask pumps the channel for the lifetime of the resource and never returns,
            // so awaiting it here would mean _initialized never completes.
            _ = StartTask(token ?? CancellationToken.None);
        }, token ?? CancellationToken.None);
    }

    public int MaxTasks { get; set; }
    public long MaxThroughput { get; set; }
    public string Name { get; }

    public async ValueTask<Job<T>> Begin(string jobTitle, long size, CancellationToken token)
    {
        await _initialized;

        var id = Interlocked.Increment(ref _nextId);
        var job = new Job<T>
        {
            ID = id,
            Description = jobTitle,
            Size = size,
            Resource = this
        };
        _tasks.TryAdd(id, job);
        await _semaphore.WaitAsync(token);
        job.Started = true;
        return job;
    }

    public void ReportNoWait(Job<T> job, int processedSize)
    {
        job.Current += processedSize;
        Interlocked.Add(ref _totalUsed, processedSize);
    }

    public void Finish(Job<T> job)
    {
        _semaphore.Release();
        _tasks.TryRemove(job.ID, out _);
    }

    /// <summary>
    ///     Records bytes against the resource, pausing the caller for as long as the throughput cap says it
    ///     should have taken.
    ///     <para>
    ///         With no cap set - which is the default for every resource, and what file hashing always runs
    ///         under - the pump does nothing with a report but add its size and complete it, so the
    ///         round-trip it costs buys nothing: a write into a channel bounded at ten, one consumer task
    ///         for the whole resource, and a <see cref="TaskCompletionSource" /> awaited per report.
    ///         <c>HashingCopy</c> reports once per megabyte read, on every hashing thread at once, so that
    ///         one consumer was the ceiling on hashing: about 3 GB/s however many threads were hashing, and
    ///         they were queueing behind it rather than reading. Hashing a 20 GiB game folder already in the
    ///         page cache went from 6.5s to 2.9s without it. Uncapped, the work the pump would have done is
    ///         done here instead.
    ///     </para>
    ///     <para>
    ///         The cap is read once. It can be changed from the settings window while this is running, and
    ///         which side of that change a single report falls on is not worth synchronising over.
    ///     </para>
    ///     <para>
    ///         A cap of zero or less is "uncapped" here rather than a cap of nothing. A negative one used to
    ///         reach the pump, which turned it into a negative <see cref="TimeSpan" />, threw out of
    ///         <see cref="Task.Delay(TimeSpan, CancellationToken)" /> and ended the pump for good, leaving
    ///         every later report on the resource waiting on a completion source nothing would ever
    ///         complete. Nothing in the product sets one - <c>ResourceLimitConfiguration</c>'s -1 default
    ///         has no readers - but this is where it stops.
    ///     </para>
    /// </summary>
    public async ValueTask Report(Job<T> job, int size, CancellationToken token)
    {
        await _initialized;

        if (MaxThroughput <= 0 || MaxThroughput == long.MaxValue)
        {
            Interlocked.Add(ref _totalUsed, size);
            return;
        }

        var tcs = new TaskCompletionSource();
        await _channel.Writer.WriteAsync(new PendingReport
        {
            Job = job,
            Size = size,
            Result = tcs
        }, token);
        await tcs.Task;
    }

    public StatusReport StatusReport =>
        new(_tasks.Count(t => t.Value.Started),
            _tasks.Count(t => !t.Value.Started),
            _totalUsed);

    private async ValueTask StartTask(CancellationToken token)
    {
        var sw = new Stopwatch();
        sw.Start();

        await foreach (var item in _channel.Reader.ReadAllAsync(token))
        {
            Interlocked.Add(ref _totalUsed, item.Size);
            if (MaxThroughput is long.MaxValue or 0)
            {
                item.Result.TrySetResult();
                sw.Restart();
                continue;
            }

            var span = TimeSpan.FromSeconds((double) item.Size / MaxThroughput);


            await Task.Delay(span, token);

            sw.Restart();

            item.Result.TrySetResult();
        }
    }

    private struct PendingReport
    {
        public Job<T> Job { get; set; }
        public int Size { get; set; }
        public TaskCompletionSource Result { get; set; }
    }
}