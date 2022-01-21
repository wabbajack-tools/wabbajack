using System.Windows.Shell;

namespace Wabbajack.App.Blazor.State;

public class TaskBarState
{
    public string Description { get; set; } = string.Empty;
    public double ProgressValue { get; set; }
    public TaskbarItemProgressState State { get; set; } = TaskbarItemProgressState.None;
}
