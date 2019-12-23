using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.Common
{
    public class WorkQueue : IDisposable
    {
        internal BlockingCollection<Func<Task>>
            Queue = new BlockingCollection<Func<Task>>(new ConcurrentStack<Func<Task>>());

        public const int UnassignedCpuId = 0;

        private static readonly AsyncLocal<int> _cpuId = new AsyncLocal<int>();
        public int CpuId => _cpuId.Value;

        public static bool WorkerThread => AsyncLocalCurrentQueue.Value != null;
        public bool IsWorkerThread => WorkerThread;
        internal static readonly AsyncLocal<WorkQueue> AsyncLocalCurrentQueue = new AsyncLocal<WorkQueue>();

        private readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        public List<Thread> Threads { get; private set; }

        private CancellationTokenSource _cancel = new CancellationTokenSource();

        // This is currently a lie, as it wires to the Utils singleton stream This is still good to have, 
        // so that logic related to a single WorkQueue can subscribe to this dummy member so that If/when we 
        // implement log messages in a non-singleton fashion, they will already be wired up properly.
        public IObservable<IStatusMessage> LogMessages => Utils.LogMessages;

        public WorkQueue(int threadCount = 0)
        {
            StartThreads(threadCount == 0 ? Environment.ProcessorCount : threadCount);
        }

        private void StartThreads(int threadCount)
        {
            ThreadCount = threadCount;
            Threads = Enumerable.Range(1, threadCount)
                .Select(idx =>
                {
                    var thread = new Thread(() => ThreadBody(idx).Wait());
                    thread.Priority = ThreadPriority.BelowNormal;
                    thread.IsBackground = true;
                    thread.Name = string.Format("Wabbajack_Worker_{0}", idx);
                    thread.Start();
                    return thread;
                }).ToList();
        }

        public int ThreadCount { get; private set; }

        private async Task ThreadBody(int idx)
        {
            _cpuId.Value = idx;
            AsyncLocalCurrentQueue.Value = this;

            try
            {
                while (true)
                {
                    Report("Waiting", 0, false);
                    if (_cancel.IsCancellationRequested) return;
                    Func<Task> f;
                    try
                    {
                        f = Queue.Take(_cancel.Token);
                    }
                    catch (Exception)
                    {
                        throw new OperationCanceledException();
                    }

                    await f();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Report(string msg, int progress, bool isWorking = true)
        {
            _Status.OnNext(
                new CPUStatus
                {
                    Progress = progress,
                    ProgressPercent = progress / 100f,
                    Msg = msg,
                    ID = _cpuId.Value,
                    IsWorking = isWorking
                });
        }

        public void QueueTask(Func<Task> a)
        {
            Queue.Add(a);
        }

        public void Dispose()
        {
            _cancel.Cancel();
            Queue?.Dispose();
        }
    }

    public class CPUStatus
    {
        public int Progress { get; internal set; }
        public float ProgressPercent { get; internal set; }
        public string Msg { get; internal set; }
        public int ID { get; internal set; }
        public bool IsWorking { get; internal set; }
    }
}
