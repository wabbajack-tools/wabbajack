using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private static bool _inited;

        public static Action<int, string, int> ReportFunction { get; private set; }
        public static Action<int, int> ReportQueueSize { get; private set; }
        public static int ThreadCount { get; private set; }
        public static List<Thread> Threads { get; private set; }

        public static void Init(Action<int, string, int> report_function, Action<int, int> report_queue_size)
        {
            ReportFunction = report_function;
            ReportQueueSize = report_queue_size;
            ThreadCount = Environment.ProcessorCount;
            if (_inited) return;
            StartThreads();
            _inited = true;

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
                CustomReportFn(progress, msg);
            else
                ReportFunction(CpuId, msg, progress);
        }

        public static void QueueTask(Action a)
        {
            Queue.Add(a);
        }

        internal static void ReportNow()
        {
            ReportQueueSize(MaxQueueSize, CurrentQueueSize);
        }
    }
}