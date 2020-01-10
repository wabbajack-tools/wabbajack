using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.Common
{
    public class WorkQueue : IDisposable
    {
        internal BlockingCollection<Func<Task>> Queue = new BlockingCollection<Func<Task>>(new ConcurrentStack<Func<Task>>());

        public const int UnassignedCpuId = 0;

        private static readonly AsyncLocal<int> _cpuId = new AsyncLocal<int>();
        public int CpuId => _cpuId.Value;

        public static bool WorkerThread => AsyncLocalCurrentQueue.Value != null;
        public bool IsWorkerThread => WorkerThread;
        internal static readonly AsyncLocal<WorkQueue> AsyncLocalCurrentQueue = new AsyncLocal<WorkQueue>();

        private readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        private int _nextCpuID = 1; // Start at 1, as 0 is "Unassigned"
        private int _desiredCount = 0;
        private List<(int CpuID, Task Task)> _tasks = new List<(int CpuID, Task Task)>();
        public int DesiredNumWorkers => _desiredCount;

        private CancellationTokenSource _shutdown = new CancellationTokenSource();

        private CompositeDisposable _disposables = new CompositeDisposable();

        // This is currently a lie, as it wires to the Utils singleton stream This is still good to have, 
        // so that logic related to a single WorkQueue can subscribe to this dummy member so that If/when we 
        // implement log messages in a non-singleton fashion, they will already be wired up properly.
        public IObservable<IStatusMessage> LogMessages => Utils.LogMessages;

        private AsyncLock _lock = new AsyncLock();

        public WorkQueue(int? threadCount = null)
            : this(Observable.Return(threadCount ?? Environment.ProcessorCount))
        {
        }

        public WorkQueue(IObservable<int> numThreads)
        {
            (numThreads ?? Observable.Return(Environment.ProcessorCount))
                .DistinctUntilChanged()
                .SelectTask(AddNewThreadsIfNeeded)
                .Subscribe()
                .DisposeWith(_disposables);
        }

        private async Task AddNewThreadsIfNeeded(int desired)
        {
            using (await _lock.Wait())
            {
                _desiredCount = desired;
                while (_desiredCount > _tasks.Count)
                {
                    var cpuID = _nextCpuID++;
                    _tasks.Add((cpuID,
                        Task.Run(async () =>
                        {
                            await ThreadBody(cpuID);
                        })));
                }
            }
        }

        private async Task ThreadBody(int cpuID)
        {
            _cpuId.Value = cpuID;
            AsyncLocalCurrentQueue.Value = this;

            try
            {
                while (true)
                {
                    Report("Waiting", 0, false);
                    if (_shutdown.IsCancellationRequested) return;
                    Func<Task> f;
                    try
                    {
                        f = Queue.Take(_shutdown.Token);
                    }
                    catch (Exception)
                    {
                        throw new OperationCanceledException();
                    }

                    await f();

                    // Check if we're currently trimming threads
                    if (_desiredCount >= _tasks.Count) continue;

                    // Noticed that we may need to shut down, lock and check again
                    using (await _lock.Wait())
                    {
                        // Check if another thread shut down before this one and got us in line
                        if (_desiredCount >= _tasks.Count) continue;

                        Report("Shutting down", 0, false);
                        // Remove this task from list
                        for (int i = 0; i < _tasks.Count; i++)
                        {
                            if (_tasks[i].CpuID == cpuID)
                            {
                                _tasks.RemoveAt(i);
                                // Shutdown thread
                                Report("Shutting down", 0, false);
                                return;
                            }
                        }
                        // Failed to remove, warn and then shutdown anyway
                        Utils.Error($"Could not remove thread from workpool with CPU ID {cpuID}");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Utils.Error(ex, "Error in WorkQueue thread.");
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
            _shutdown.Cancel();
            _disposables.Dispose();
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
