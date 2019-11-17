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

        internal static bool WorkerThread => CurrentQueue != null;
        [ThreadStatic] internal static WorkQueue CurrentQueue;

        private static readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        public static List<Thread> Threads { get; private set; }

        public WorkQueue(int threadCount = 0)
        {
            StartThreads(threadCount == 0 ? Environment.ProcessorCount : threadCount);
        }

        private void StartThreads(int threadCount)
        {
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

        private void ThreadBody(int idx)
        {
            CpuId = idx;
            CurrentQueue = this;

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