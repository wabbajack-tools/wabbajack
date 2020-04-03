using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Wabbajack.Common.StatusFeed;

[assembly: InternalsVisibleTo("Wabbajack.Test")]
namespace Wabbajack.Common
{
    public class WorkQueue : IDisposable
    {
        internal AsyncBlockingCollection<Func<Task>> Queue = new AsyncBlockingCollection<Func<Task>>();

        public const int UnassignedCpuId = 0;

        private static readonly AsyncLocal<int> _cpuId = new AsyncLocal<int>();
        public int CpuId => _cpuId.Value;

        public static bool WorkerThread => AsyncLocalCurrentQueue.Value != null;
        public bool IsWorkerThread => WorkerThread;
        internal static readonly AsyncLocal<WorkQueue?> AsyncLocalCurrentQueue = new AsyncLocal<WorkQueue?>();

        private readonly Subject<CPUStatus> _Status = new Subject<CPUStatus>();
        public IObservable<CPUStatus> Status => _Status;

        private int _nextCpuID = 1; // Start at 1, as 0 is "Unassigned"
        // Public for testing reasons
        public Dictionary<int, Task> _tasks = new Dictionary<int, Task>();
        public int DesiredNumWorkers { get; private set; } = 0;

        private CancellationTokenSource _shutdown = new CancellationTokenSource();

        private CompositeDisposable _disposables = new CompositeDisposable();

        // This is currently a lie, as it wires to the Utils singleton stream This is still good to have, 
        // so that logic related to a single WorkQueue can subscribe to this dummy member so that If/when we 
        // implement log messages in a non-singleton fashion, they will already be wired up properly.
        public IObservable<IStatusMessage> LogMessages => Utils.LogMessages;

        private AsyncLock _lock = new AsyncLock();

        private readonly BehaviorSubject<(int DesiredCPUs, int CurrentCPUs)> _cpuCountSubj = new BehaviorSubject<(int DesiredCPUs, int CurrentCPUs)>((0, 0));
        public IObservable<(int CurrentCPUs, int DesiredCPUs)> CurrentCpuCount => _cpuCountSubj;

        private readonly Subject<IObservable<int>> _activeNumThreadsObservable = new Subject<IObservable<int>>();

        public static TimeSpan PollMS = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Creates a WorkQueue with the given number of threads
        /// </summary>
        /// <param name="threadCount">Number of threads for the WorkQueue to have.  Null represents default, which is the Processor count of the machine.</param>
        public WorkQueue(int? threadCount = null)
            : this(Observable.Return(threadCount ?? Environment.ProcessorCount))
        {
        }

        /// <summary>
        /// Creates a WorkQueue whos number of threads is determined by the given observable
        /// </summary>
        /// <param name="numThreads">Driving observable that determines how many threads should be actively pulling jobs from the queue</param>
        public WorkQueue(IObservable<int> numThreads)
        {
            // Hook onto the number of active threads subject, and subscribe to it for changes
            _activeNumThreadsObservable
                // Select the latest driving observable
                .Select(x => x ?? Observable.Return(Environment.ProcessorCount))
                .Switch()
                .DistinctUntilChanged()
                // Add new threads if it increases
                .SelectTask(AddNewThreadsIfNeeded)
                .Subscribe()
                .DisposeWith(_disposables);
            // Set the incoming driving observable to be active
            SetActiveThreadsObservable(numThreads);
        }

        /// <summary>
        /// Sets the driving observable that determines how many threads should be actively pulling jobs from the queue
        /// </summary>
        /// <param name="numThreads">Driving observable that determines how many threads should be actively pulling jobs from the queue</param>
        public void SetActiveThreadsObservable(IObservable<int> numThreads)
        {
            _activeNumThreadsObservable.OnNext(numThreads);
        }

        private async Task AddNewThreadsIfNeeded(int desired)
        {
            using (await _lock.Wait())
            {
                DesiredNumWorkers = desired;
                while (DesiredNumWorkers > _tasks.Count)
                {
                    var cpuID = _nextCpuID++;
                    _tasks[cpuID] = Task.Run(async () =>
                    {
                        await ThreadBody(cpuID);
                    });
                }
                _cpuCountSubj.OnNext((_tasks.Count, DesiredNumWorkers));
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
                    Report("Waiting", Percent.Zero, false);
                    if (_shutdown.IsCancellationRequested) return;


                    Func<Task> f;
                    bool got;
                    try
                    {
                        (got, f) = await Queue.TryTake(PollMS, _shutdown.Token);
                    }
                    catch (Exception)
                    {
                        throw new OperationCanceledException();
                    }

                    if (got)
                    {
                        await f();
                    }

                    // Check if we're currently trimming threads
                    if (DesiredNumWorkers >= _tasks.Count) continue;

                    // Noticed that we may need to shut down, lock and check again
                    using (await _lock.Wait())
                    {
                        // Check if another thread shut down before this one and got us back to the desired amount already
                        if (DesiredNumWorkers >= _tasks.Count) continue;

                        // Shutdown
                        if (!_tasks.Remove(cpuID))
                        {
                            Utils.Error($"Could not remove thread from workpool with CPU ID {cpuID}");
                        }
                        Report("Shutting down", Percent.Zero, false);
                        _cpuCountSubj.OnNext((_tasks.Count, DesiredNumWorkers));
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

        public void Report(string msg, Percent progress, bool isWorking = true)
        {
            _Status.OnNext(
                new CPUStatus
                {
                    ProgressPercent = progress,
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
        public Percent ProgressPercent { get; internal set; }
        public string Msg { get; internal set; } = string.Empty;
        public int ID { get; internal set; }
        public bool IsWorking { get; internal set; }
    }
}
