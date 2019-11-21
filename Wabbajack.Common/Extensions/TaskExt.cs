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
    }
}
