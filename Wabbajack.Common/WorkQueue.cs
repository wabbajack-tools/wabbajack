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
        internal static BlockingCollection<Action>
            Queue = new BlockingCollection<Action>(new ConcurrentStack<Action>());

        [ThreadStatic] private static int CpuId;

        [ThreadStatic] internal static bool WorkerThread;

        [ThreadStatic] public static Action<int, string> CustomReportFn;

        public static int MaxQueueSize;
        public static int CurrentQueueSize;

        private readonly static Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public static IObservable<CPUStatus> Status => _Status;
        private readonly static Subject<(int Current, int Max)> _QueueSize = new Subject<(int Current, int Max)>();
        public static IObservable<(int Current, int Max)> QueueSize => _QueueSize;
        public static int ThreadCount { get; } = Environment.ProcessorCount;
        public static List<Thread> Threads { get; private set; }

        static WorkQueue()
        {
            StartThreads();
        }

        private static void StartThreads()
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

        private static void ThreadBody(int idx)
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

        public static void Report(string msg, int progress)
        {
            if (CustomReportFn != null)
            {
                CustomReportFn(progress, msg);
            }
            else
            {
                _Status.OnNext(
                    new CPUStatus
                    {
                        Progress = progress,
                        Msg = msg,
                        ID = CpuId
                    });
            }
        }

        public static void QueueTask(Action a)
        {
            Queue.Add(a);
        }

        internal static void ReportNow()
        {
            _QueueSize.OnNext((MaxQueueSize, CurrentQueueSize));
        }

        public static void Init()
        {
            Init((a, b, c) => { }, (a, b) => { });
        }
    }

    public class CPUStatus
    {
        public int Progress { get; internal set; }
        public string Msg { get; internal set; }
        public int ID { get; internal set; }
    }
}