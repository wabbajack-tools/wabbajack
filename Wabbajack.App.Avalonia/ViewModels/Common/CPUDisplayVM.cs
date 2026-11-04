using System;
using ReactiveUI.SourceGenerators;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Avalonia.ViewModels.Common;

/// <summary>One running job as CpuLineView shows it; filled in by <see cref="ResourceMonitor" />.</summary>
public partial class CPUDisplayVM : ViewModel
{
    [Reactive]
    public partial ulong ID { get; set; }
    [Reactive]
    public partial DateTime StartTime { get; set; }
    [Reactive]
    public partial bool IsWorking { get; set; }
    [Reactive]
    public partial string Msg { get; set; }
    [Reactive]
    public partial Percent ProgressPercent { get; set; }
}
