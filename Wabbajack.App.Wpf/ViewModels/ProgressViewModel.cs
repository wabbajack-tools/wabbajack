using ReactiveUI.Fody.Helpers;
using Wabbajack.RateLimiter;

namespace Wabbajack;

public abstract class ProgressViewModel : ViewModel, IProgressVM
{
    [Reactive] public Step CurrentStep { get; set; }
    [Reactive] public ProgressState ProgressState { get; set; }
    [Reactive] public string ConfigurationText { get; set; }
    [Reactive] public string ProgressText { get; set; }
    [Reactive] public Percent ProgressPercent { get; set; }
}
