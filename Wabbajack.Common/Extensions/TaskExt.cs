using System;
using System.Threading.Tasks;

namespace Wabbajack
{
    public static class TaskExt
    {
        public static async void FireAndForget(this Task task, Action<Exception> onException = null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            when (onException != null)
            {
                onException(ex);
            }
        }

        /// <summary>
        /// returns a Task that will await the input task, but fire an action if it takes longer than a given time
        /// </summary>
        public static async Task TimeoutButContinue(this Task task, TimeSpan timeout, Action actionOnTimeout)
        {
            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
            if (completedTask == timeoutTask)
            {
                actionOnTimeout();
                await task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// returns a Task that will await the input task, but fire an action if it takes longer than a given time
        /// </summary>
        public static async Task<TRet> TimeoutButContinue<TRet>(this Task<TRet> task, TimeSpan timeout, Action actionOnTimeout)
        {
            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
            if (completedTask == timeoutTask)
            {
                actionOnTimeout();
            }
            return await task.ConfigureAwait(false);
        }
    }
}
