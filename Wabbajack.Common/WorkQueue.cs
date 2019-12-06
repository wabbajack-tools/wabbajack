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

        [ThreadStatic] private static int CpuId;

        internal static bool WorkerThread => CurrentQueue != null;
        [ThreadStatic] internal static WorkQueue CurrentQueue;

        private readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        private static readonly Subject<IStatusMessage> _messages = new Subject<IStatusMessage>();
        public IObservable<IStatusMessage> Messages => _messages;

        public static List<Thread> Threads { get; private set; }

        public WorkQueue(int threadCount = 0)
        {
            StartThreads(threadCount == 0 ? Environment.ProcessorCount : threadCount);
        }

        public void Log(IStatusMessage msg)
        {
            _messages.OnNext(msg);
        }

        public void Log(string msg)
        {
            _messages.OnNext(new GenericInfo(msg));
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
            CpuId = idx;
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
                    ID = CpuId,
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
