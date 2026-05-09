using ReactiveUI.SourceGenerators;
using Wabbajack.RateLimiter;

namespace Wabbajack;

public abstract partial class ProgressViewModel : ViewModel, IProgressVM
{
    [Reactive] public partial Step CurrentStep { get; set; }
    [Reactive] public partial ProgressState ProgressState { get; set; }
    [Reactive] public partial string ConfigurationText { get; set; }
    [Reactive] public partial string ProgressText { get; set; }
    [Reactive] public partial Percent ProgressPercent { get; set; }
}
