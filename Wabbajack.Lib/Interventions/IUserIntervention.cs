using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.Lib
{
    /// <summary>
    /// Defines a message that requires user interaction. The user must perform some action
    /// or make a choice. 
    /// </summary>
    public interface IUserIntervention : IStatusMessage, IReactiveObject
    {
        /// <summary>
        /// The user didn't make a choice, so this action should be aborted
        /// </summary>
        void Cancel();

        /// <summary>
        /// Whether the interaction has been handled and no longer needs attention
        /// Note: This needs to be Reactive so that users can monitor its status
        /// </summary>
        bool Handled { get; }

        /// <summary>
        /// WorkQueue job ID that is blocking on this intervention
        /// </summary>
        int CpuID { get; }
    }
}
