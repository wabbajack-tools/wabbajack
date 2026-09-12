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

    public async ValueTask Report(Job<T> job, int size, CancellationToken token)
    {
        await _initialized;

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