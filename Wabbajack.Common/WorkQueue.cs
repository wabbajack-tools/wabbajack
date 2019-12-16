using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common.CSP;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.Common
{
    public class WorkQueue : IDisposable
    {
        internal IChannel<Func<Task>, Func<Task>>
            Queue = Channel.Create<Func<Task>>(1024);

        public const int UnassignedCpuId = 0;

        private static readonly AsyncLocal<int> _cpuId = new AsyncLocal<int>();
        public int CpuId => _cpuId.Value;

        internal static bool WorkerThread => ThreadLocalCurrentQueue.Value != null;
        internal static readonly ThreadLocal<WorkQueue> ThreadLocalCurrentQueue = new ThreadLocal<WorkQueue>();
        internal static readonly AsyncLocal<WorkQueue> AsyncLocalCurrentQueue = new AsyncLocal<WorkQueue>();

        private readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        public List<Task> Tasks { get; private set; }

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
            Tasks = Enumerable.Range(1, threadCount)
                .Select(idx =>
                {
                    return Task.Run(() => TaskBody(idx));
                }).ToList();
        }

        public int ThreadCount { get; private set; }

        private async Task TaskBody(int idx)
        {
            _cpuId.Value = idx;
            ThreadLocalCurrentQueue.Value = this;
            AsyncLocalCurrentQueue.Value = this;

            try
            {
                while (true)
                {
                    Report("Waiting", 0, false);

                    var (isOpen, f) = await Queue.Take();
                    if (!isOpen) break;

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

        public async ValueTask QueueTask(Func<Task> a)
        {
            var isOpen = await Queue.Put(a);
            if (!isOpen)
                throw new InvalidOperationException("Queue is closed");
        }

        public void Dispose()
        {
            Queue.Close();
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
