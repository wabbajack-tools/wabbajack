using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public enum AsyncResult : int
    {
        /// <summary>
        /// The channel was closed, so the returned value is meaningless
        /// </summary>
        Closed,

        /// <summary>
        /// The handler was canceled, so the returned value is meaningless
        /// </summary>
        Canceled,

        /// <summary>
        /// The callback was enqueued into the pending operations buffer, return value is useless
        /// </summary>
        Enqueued,

        /// <summary>
        /// The operation passed on the current thread, return the current value as the response value
        /// </summary>
        Completed
    }
}
