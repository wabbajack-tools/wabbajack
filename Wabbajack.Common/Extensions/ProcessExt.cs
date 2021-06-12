using System;
using System.Diagnostics;

namespace Wabbajack.Common
{
    public static class ProcessExt
    {
        public static void WaitForExitAndWarn(this Process process, TimeSpan warningTimeout, string processTitle)
        {
            if (!process.WaitForExit((int)warningTimeout.TotalMilliseconds))
            {
                Utils.Status($"{processTitle} - Taking a long time to exit.", alsoLog: true);
                process.WaitForExit();
                Utils.Status($"{processTitle} - Exited after a long period.", alsoLog: true);
            }
        }
    }
}
