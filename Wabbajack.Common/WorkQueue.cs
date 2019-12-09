using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.Common
{
    public class WorkQueue : IDisposable
    {
        internal BlockingCollection<Action>
            Queue = new BlockingCollection<Action>(new ConcurrentStack<Action>());

        public const int UnassignedCpuId = -1;

        [ThreadStatic] private static int _cpuId = UnassignedCpuId;
        public static int CpuId => _cpuId;

        internal static bool WorkerThread => CurrentQueue != null;
        [ThreadStatic] internal static WorkQueue CurrentQueue;

        private readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        public static List<Thread> Threads { get; private set; }

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
            Threads = Enumerable.Range(0, threadCount)
                .Select(idx =>
                {
                    var thread = new Thread(() => ThreadBody(idx));
                    thread.Priority = ThreadPriority.BelowNormal;
                    thread.IsBackground = true;
                    thread.Name = string.Format("Wabbajack_Worker_{0}", idx);
                    thread.Start();
                    return thread;
                }).ToList();
        }

        public int ThreadCount { get; private set; }

        private void ThreadBody(int idx)
        {
            _cpuId = idx;
            CurrentQueue = this;

            while (true)
            {
                Report("Waiting", 0, false);
                var f = Queue.Take();
                f();
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
                    ID = _cpuId,
                    IsWorking = isWorking
                });
        }

        public void QueueTask(Action a)
        {
            Queue.Add(a);
        }

        public void Shutdown()
        {
            Threads.Do(th => th.Abort());
            Threads.Do(th => th.Join());
        }

        public void Dispose()
        {
            Shutdown();
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
