using System;
using ReactiveUI.Fody.Helpers;
using Wabbajack;
using Wabbajack.RateLimiter;

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
        public Percent ProgressPercent { get; set; }

        public CPUDisplayVM()
        {
        }

        public CPUDisplayVM(IJob cpu)
        {
            AbsorbStatus(cpu);
        }

        public void AbsorbStatus(IJob cpu)
        {
            /* TODO
            bool starting = cpu.IsWorking && !IsWorking;
            if (starting)
            {
                StartTime = DateTime.Now;
            }

            ID = cpu.;
            Msg = cpu.Msg;
            ProgressPercent = cpu.ProgressPercent;
            IsWorking = cpu.IsWorking;
            */
        }
    }
}
