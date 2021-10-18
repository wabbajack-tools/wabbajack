using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Wabbajack.RateLimiter
{
    public class Resource<T> : IResource<T>
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Channel<PendingReport> _channel;
        private readonly ConcurrentDictionary<ulong, Job<T>> _tasks;
        private ulong _nextId = 0;
        private long _totalUsed = 0;
        private readonly int _maxTasks;
        private readonly long _maxThroughput;
        private readonly string _humanName;
        public string Name => _humanName;
        
        

        public Resource(string humanName, int maxTasks = Int32.MaxValue, long maxThroughput = long.MaxValue)
        {
            _humanName = humanName;
            _maxTasks = maxTasks;
            _maxThroughput = maxThroughput;

            _semaphore = new SemaphoreSlim(_maxTasks);
            _channel = Channel.CreateBounded<PendingReport>(10);
            _tasks = new ();

            var tsk = StartTask(CancellationToken.None);
        }

        private async ValueTask StartTask(CancellationToken token)
        {
            var sw = new Stopwatch();
            sw.Start();

            await foreach (var item in _channel.Reader.ReadAllAsync(token))
            {
                Interlocked.Add(ref _totalUsed, item.Size);
                if (_maxThroughput == long.MaxValue)
                {
                    item.Result.TrySetResult();
                    sw.Restart();
                    continue;
                }
                
                var span = TimeSpan.FromSeconds((double)item.Size / _maxThroughput);
                

                await Task.Delay(span, token);
                
                sw.Restart();
                
                item.Result.TrySetResult();
            }
        }

        public async ValueTask<Job<T>> Begin(string jobTitle, long size, CancellationToken token)
        {
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
            var tcs = new TaskCompletionSource();
            await _channel.Writer.WriteAsync(new PendingReport
            {
                Job = job,
                Size = size,
                Result = tcs
            }, token);
            await tcs.Task;
        }

        struct PendingReport
        {
            public Job<T> Job { get; set; }
            public int Size { get; set; }
            public TaskCompletionSource Result { get; set; }
        }

        public StatusReport StatusReport =>
            new(_tasks.Count(t => t.Value.Started),
            _tasks.Count(t => !t.Value.Started),
            _totalUsed);


    }
}