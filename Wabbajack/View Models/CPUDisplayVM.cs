using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack
{
    public class CPUDisplayVM
    {
        public CPUStatus Status { get; set; }
        public DateTime StartTime { get; set; }

        public void AbsorbStatus(CPUStatus cpu)
        {
            bool starting = cpu.IsWorking && ((!Status?.IsWorking) ?? true);
            Status = cpu;
            if (starting)
            {
                StartTime = DateTime.Now;
            }
        }
    }
}
