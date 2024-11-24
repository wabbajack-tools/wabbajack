using Wabbajack.RateLimiter;

namespace Wabbajack;

public enum Step
{
    Configuration, // Configuration is enlarged
    Busy,          // Progress bar is enlarged
    Done           // Both are same size
}
public enum ProgressState
{
    Normal,       // Progress bar is not highlighted
    Success,      // Operation succeeded, progress bar gets highlighted
    Error         // Operation failed, progress bar gets highlighted
}

public interface IProgressVM
{
    public Step CurrentStep { get; set; }
    public ProgressState ProgressState { get; set; }
    public string ConfigurationText { get; set; }
    public string ProgressText { get; set; }
    public Percent ProgressPercent { get; set; }
}
