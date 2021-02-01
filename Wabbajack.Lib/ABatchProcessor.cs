using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public abstract class ABatchProcessor : IBatchProcessor
    {
        public WorkQueue Queue { get; } = new WorkQueue();

        public int DownloadThreads { get; set; } = Environment.ProcessorCount <= 8 ? Environment.ProcessorCount : 8;
        public int DiskThreads { get; set; } = Environment.ProcessorCount;
        public bool ReduceHDDThreads { get; set; } = true;
        public bool FavorPerfOverRam { get; set; } = false;
        
        public Context VFS { get; }

        protected StatusUpdateTracker UpdateTracker { get; }

        private Subject<Percent> _percentCompleted { get; } = new Subject<Percent>();

        /// <summary>
        /// The current progress of the entire processing system on a scale of 0.0 to 1.0
        /// </summary>
        public IObservable<Percent> PercentCompleted => _percentCompleted;

        private Subject<string> _textStatus { get; } = new Subject<string>();

        /// <summary>
        /// The current status of the processor as a text string
        /// </summary>
        public IObservable<string> TextStatus => _textStatus;

        private Subject<CPUStatus> _queueStatus { get; } = new Subject<CPUStatus>();
        public IObservable<CPUStatus> QueueStatus => _queueStatus;

        private Subject<IStatusMessage> _logMessages { get; } = new Subject<IStatusMessage>();
        public IObservable<IStatusMessage> LogMessages => _logMessages;

        private Subject<bool> _isRunning { get; } = new Subject<bool>();
        public IObservable<bool> IsRunning => _isRunning;

        private int _started;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        private readonly CompositeDisposable _subs = new CompositeDisposable();

        // WorkQueue settings
        public BehaviorSubject<bool> ManualCoreLimit = new BehaviorSubject<bool>(true);
        public BehaviorSubject<byte> MaxCores = new BehaviorSubject<byte>(byte.MaxValue);
        public BehaviorSubject<Percent> TargetUsagePercent = new BehaviorSubject<Percent>(Percent.One);
        public Subject<int> DesiredThreads { get; set; }

        public ABatchProcessor(int steps)
        {
            UpdateTracker = new StatusUpdateTracker(steps);
            VFS = new Context(Queue) { UpdateTracker = UpdateTracker };
            Queue.Status.Subscribe(_queueStatus)
                .DisposeWith(_subs);
            Queue.LogMessages.Subscribe(_logMessages)
                .DisposeWith(_subs);
            UpdateTracker.Progress.Subscribe(_percentCompleted);
            UpdateTracker.StepName.Subscribe(_textStatus);
            DesiredThreads = new Subject<int>();
            Queue.SetActiveThreadsObservable(DesiredThreads);
        }

        
        /// <summary>
        /// Gets the recommended maximum number of threads that should be used for the current machine.
        /// This will either run a heavy processing job to do the measurement in the current folder, or refer to caches.
        /// </summary>
        /// <returns>Recommended maximum number of threads to use</returns>
        public async Task<int> RecommendQueueSize()
        {
            const ulong GB = (1024 * 1024 * 1024);
            // Most of the heavy lifting is done on the scratch disk, so we'll use the value from that disk
            var memory = Utils.GetMemoryStatus();
            // Assume roughly 2GB of ram needed to extract each 7zip archive, and then leave 2GB for the OS. If calculation is lower or equal to 1 GB, use 1GB
            var basedOnMemory = Math.Max((memory.ullTotalPhys - (2 * GB)) / (2 * GB), 1);
            var scratchSize = await RecommendQueueSize(AbsolutePath.EntryPoint);
            var result = Math.Min((int)basedOnMemory, (int)scratchSize);
            Utils.Log($"Recommending a queue size of {result} based on disk performance, number of cores, and {((long)memory.ullTotalPhys).ToFileSizeString()} of system RAM");
            return result;
        }

        /// <summary>
        /// Gets the recommended maximum number of threads that should be used for the current machine.
        /// This will either run a heavy processing job to do the measurement in the specified folder, or refer to caches.
        /// 
        /// If the folder does not exist, it will be created, and not cleaned up afterwards.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns>Recommended maximum number of threads to use</returns>
        public static async Task<int> RecommendQueueSize(AbsolutePath folder)
        {
            using var queue = new WorkQueue();
            
            Utils.Log($"Benchmarking {folder}");
            var raw_speed = await Utils.TestDiskSpeed(queue, folder);
            Utils.Log($"{raw_speed.ToFileSizeString()}/sec for {folder}");
            int speed = (int)(raw_speed / 1024 / 1024);

            // Less than 200, it's probably a HDD, so we can't go higher than 2
            if (speed < 200) return 2;
            // SATA SSD, so stick with 8 thread maximum
            if (speed < 600) return Math.Min(Environment.ProcessorCount, 8);
            // Anything higher is probably a NVME or a really good SSD, so take off the reins
            return Environment.ProcessorCount;
        }

        /// <summary>
        /// Constructs an observable of the number of threads to be used
        /// 
        /// Takes in a recommended amount (based off measuring the machine capabilities), and combines that with user preferences stored in subjects.
        /// 
        /// As user preferences change, the number of threads gets recalculated in the resulting observable
        /// </summary>
        /// <param name="recommendedCount">Maximum recommended number of threads</param>
        /// <returns>Observable of number of threads to use based off recommendations and user preferences</returns>
        public IObservable<int> ConstructDynamicNumThreads(int recommendedCount)
        {
            return Observable.CombineLatest(
                ManualCoreLimit,
                MaxCores,
                TargetUsagePercent,
                (manual, max, target) => CalculateThreadsToUse(recommendedCount, manual, max, target.Value));
        }

        /// <summary>
        /// Calculates the number of threads to use, based off recommended values and user preferences
        /// </summary>
        public static int CalculateThreadsToUse(
            int recommendedCount,
            bool manual,
            byte manualMax,
            double targetUsage)
        {
            if (manual)
            {
                if (recommendedCount > manualMax)
                {
                    Utils.Log($"Only using {manualMax} due to user preferences.");
                }
                return Math.Max(1, Math.Min(manualMax, recommendedCount));
            }
            else if (targetUsage < 1.0d && targetUsage >= 0d)
            {
                var ret = (int)Math.Ceiling(recommendedCount * targetUsage);
                return Math.Max(1, ret);
            }
            return recommendedCount;
        }

        protected abstract Task<bool> _Begin(CancellationToken cancel);
        public Task<bool> Begin()
        {
            if (1 == Interlocked.CompareExchange(ref _started, 1, 1))
            {
                throw new InvalidDataException("Can't start the processor twice");
            }

            Utils.Log("Starting Installer Task");
            return Task.Run(async () =>
            { 
                try
                {
                    Utils.Log("Installation has Started");
                    _isRunning.OnNext(true);
                    return await _Begin(_cancel.Token);
                }
                finally
                {
                    Utils.Log("Vacuuming databases");
                    HashCache.VacuumDatabase();
                    PatchCache.VacuumDatabase();
                    VirtualFile.VacuumDatabase();
                    Utils.Log("Vacuuming completed");
                    _isRunning.OnNext(false);
                }
            });
        }

        public void Dispose()
        {
            _cancel.Cancel();
            Queue?.Dispose();
            _isRunning.OnNext(false);
        }

        public void Abort()
        {
            _cancel.Cancel();
        }

        public void Add(IDisposable disposable) => _subs.Add(disposable);
    }
}
