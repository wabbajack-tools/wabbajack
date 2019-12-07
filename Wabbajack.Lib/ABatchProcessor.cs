using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public abstract class ABatchProcessor : IBatchProcessor
    {
        public WorkQueue Queue { get; private set; }

        public Context VFS { get; private set; }

        protected StatusUpdateTracker UpdateTracker { get; private set; }

        private Subject<float> _percentCompleted { get; } = new Subject<float>();

        /// <summary>
        /// The current progress of the entire processing system on a scale of 0.0 to 1.0
        /// </summary>
        public IObservable<float> PercentCompleted => _percentCompleted;

        private Subject<string> _textStatus { get; } = new Subject<string>();

        /// <summary>
        /// The current status of the processor as a text string
        /// </summary>
        public IObservable<string> TextStatus => _textStatus;

        private Subject<CPUStatus> _queueStatus { get; } = new Subject<CPUStatus>();
        public IObservable<CPUStatus> QueueStatus => _queueStatus;

        private Subject<bool> _isRunning { get; } = new Subject<bool>();
        public IObservable<bool> IsRunning => _isRunning;
        
        private Thread _processorThread { get; set; }

        private int _configured;
        private int _started;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        protected void ConfigureProcessor(int steps, int threads = 0)
        {
            if (1 == Interlocked.CompareExchange(ref _configured, 1, 1))
            {
                throw new InvalidDataException("Can't configure a processor twice");
            }
            Queue = new WorkQueue(threads);
            UpdateTracker = new StatusUpdateTracker(steps);
            Queue.Status.Subscribe(_queueStatus);
            UpdateTracker.Progress.Subscribe(_percentCompleted);
            UpdateTracker.StepName.Subscribe(_textStatus);
            VFS = new Context(Queue) { UpdateTracker = UpdateTracker };
        }

        public static int RecommendQueueSize(string folder)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            using (var queue = new WorkQueue())
            {
                Utils.Log($"Benchmarking {folder}");
                var raw_speed = Utils.TestDiskSpeed(queue, folder);
                Utils.Log($"{raw_speed.ToFileSizeString()}/sec for {folder}");
                int speed = (int)(raw_speed / 1024 / 1024);

                // Less than 100MB/sec, stick with two threads.
                return speed < 100 ? 2 : Math.Min(Environment.ProcessorCount, speed / 100 * 2);
            }
        }

        protected abstract bool _Begin(CancellationToken cancel);
        public Task<bool> Begin()
        {
            if (1 == Interlocked.CompareExchange(ref _started, 1, 1))
            {
                throw new InvalidDataException("Can't start the processor twice");
            }

            _isRunning.OnNext(true);
            var _tcs = new TaskCompletionSource<bool>();

            _processorThread = new Thread(() =>
            {
                try
                {
                    _tcs.SetResult(_Begin(_cancel.Token));
                }
                catch (Exception ex)
                {
                    _tcs.SetException(ex);
                }
                finally
                {
                    _isRunning.OnNext(false);
                }
            });
            _processorThread.Priority = ThreadPriority.BelowNormal;
            _processorThread.Start();
            return _tcs.Task;
        }

        public void Dispose()
        {
            _cancel.Cancel();
            Queue?.Dispose();
            _isRunning.OnNext(false);
        }
    }
}
