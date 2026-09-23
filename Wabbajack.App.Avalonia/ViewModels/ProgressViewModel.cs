using ReactiveUI.SourceGenerators;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>Which of the title bar's two boxes is the wide one.</summary>
public enum Step
{
    /// <summary>The configuration box is wide.</summary>
    Configuration,

    /// <summary>The progress box is wide.</summary>
    Busy,

    /// <summary>Both are sized to their text.</summary>
    Done
}

public enum ProgressState
{
    Normal,
    Success,
    Error
}

/// <summary>A screen that puts its progress in the title bar, as the installer does.</summary>
public interface IProgressVM
{
    Step CurrentStep { get; set; }
    ProgressState ProgressState { get; set; }
    string ConfigurationText { get; set; }
    string ProgressText { get; set; }
    Percent ProgressPercent { get; set; }
}

public abstract partial class ProgressViewModel : ViewModel, IProgressVM
{
    [Reactive] public partial Step CurrentStep { get; set; }
    [Reactive] public partial ProgressState ProgressState { get; set; }
    [Reactive] public partial string ConfigurationText { get; set; }
    [Reactive] public partial string ProgressText { get; set; }
    [Reactive] public partial Percent ProgressPercent { get; set; }
}
