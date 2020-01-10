using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CPUDisplayVM : ViewModel
    {
        [Reactive]
        public int ID { get; set; }
        [Reactive]
        public DateTime StartTime { get; set; }
        [Reactive]
        public bool IsWorking { get; set; }
        [Reactive]
        public string Msg { get; set; }
        [Reactive]
        public float ProgressPercent { get; set; }

        public CPUDisplayVM()
        {
        }

        public CPUDisplayVM(CPUStatus cpu)
        {
            AbsorbStatus(cpu);
        }

        public void AbsorbStatus(CPUStatus cpu)
        {
            bool starting = cpu.IsWorking && !IsWorking;
            if (starting)
            {
                StartTime = DateTime.Now;
            }

            ID = cpu.ID;
            Msg = cpu.Msg;
            ProgressPercent = cpu.ProgressPercent;
            IsWorking = cpu.IsWorking;
        }
    }
}
