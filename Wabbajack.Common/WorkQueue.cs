using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common.CSP;

namespace Wabbajack.Common
{
    public class WorkQueue : IDisposable
    {
        internal IChannel<Action, Action> Queue = Channel.Create<Action>(1024);

        [ThreadStatic] private static int CpuId;

        internal static bool WorkerThread => CurrentQueue != null;
        [ThreadStatic] internal static WorkQueue CurrentQueue;

        private static readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        public static List<Task> Threads { get; private set; }

        private CancellationTokenSource cancel = new CancellationTokenSource();

        public WorkQueue(int threadCount = 0)
        {
            StartThreads(threadCount == 0 ? Environment.ProcessorCount : threadCount);
        }

        private void StartThreads(int threadCount)
        {
            ThreadCount = threadCount;
            Threads = Enumerable.Range(0, threadCount)
                .Select(idx =>
                {
                    return Task.Run(() => ThreadBody(idx));
                }).ToList();
        }

        public int ThreadCount { get; private set; }

        private async Task ThreadBody(int idx)
        {
            CpuId = idx;
            CurrentQueue = this;

            while (true)
            {
                var (is_open, f) = await Queue.Take();
                if (!is_open) break;
                f();
            }
        }

        public void Report(string msg, int progress)
        {
            _Status.OnNext(
                new CPUStatus
                {
                    Progress = progress,
                    Msg = msg,
                    ID = CpuId
                });
        }

        public void QueueAll(IEnumerable<Action> a)
        {
            a.OntoChannel(Queue);
        }

        public void Shutdown()
        {
            Queue.Close();
        }

        public void Dispose()
        {
            Shutdown();
        }
    }

    public class CPUStatus
    {
        public int Progress { get; internal set; }
        public string Msg { get; internal set; }
        public int ID { get; internal set; }
    }
}
