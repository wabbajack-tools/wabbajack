using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Wabbajack.Common
{
    public class WorkQueue
    {
        internal BlockingCollection<Action>
            Queue = new BlockingCollection<Action>(new ConcurrentStack<Action>());

        [ThreadStatic] private static int CpuId;

        [ThreadStatic] internal static bool WorkerThread;

        private readonly static Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;
        private readonly Subject<(int Current, int Max)> _QueueSize = new Subject<(int Current, int Max)>();
        public IObservable<(int Current, int Max)> QueueSize => _QueueSize;
        public static int ThreadCount { get; } = Environment.ProcessorCount;
        public static List<Thread> Threads { get; private set; }

        public WorkQueue()
        {
            StartThreads();
        }

        private void StartThreads()
        {
            Threads = Enumerable.Range(0, ThreadCount)
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

        private void ThreadBody(int idx)
        {
            CpuId = idx;
            WorkerThread = true;

            while (true)
            {
                Report("Waiting", 0);
                var f = Queue.Take();
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

        public void QueueTask(Action a)
        {
            Queue.Add(a);
        }

        public void Shutdown()
        {
            Threads.Do(th => th.Abort());
        }
    }

    public class CPUStatus
    {
        public int Progress { get; internal set; }
        public string Msg { get; internal set; }
        public int ID { get; internal set; }
    }
}