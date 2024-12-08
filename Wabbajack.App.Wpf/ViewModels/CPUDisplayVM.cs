using System;
using ReactiveUI.Fody.Helpers;
using Wabbajack.RateLimiter;

namespace Wabbajack;

public class CPUDisplayVM : ViewModel
{
    [Reactive]
    public ulong ID { get; set; }
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
}
