using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
