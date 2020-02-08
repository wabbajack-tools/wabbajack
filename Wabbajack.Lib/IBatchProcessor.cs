using System;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    /// <summary>
    /// Wabbajack runs mostly as a batch processor of sorts, we have a list of tasks we need to perform
    /// and the Compilers/Installers run throught those tasks one at a time. At any given moment the processor
    /// will be using multiple threads to complete that task. This interface defines a common implementation of
    /// all reporting functionality of both the compilers and installers.
    ///
    /// These processors are disposible because they contain WorkQueues which must be properly shutdown to keep
    /// from leaking threads. 
    /// </summary>
    public interface IBatchProcessor : IDisposable
    {
        /// <summary>
        /// The current progress of the entire processing system on a scale of 0.0 to 1.0
        /// </summary>
        IObservable<Percent> PercentCompleted { get; }

        /// <summary>
        /// The current status of the processor as a text string
        /// </summary>
        IObservable<string> TextStatus { get; }
        
        /// <summary>
        /// The status of the processor's work queue
        /// </summary>
        IObservable<CPUStatus> QueueStatus { get; }

        IObservable<bool> IsRunning { get; }

        /// <summary>
        /// Begin processing
        /// </summary>
        Task<bool> Begin();
    }
}
