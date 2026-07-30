namespace Wabbajack.Abstractions;

// UI-framework-neutral replacement for System.Windows.Shell.TaskbarItemProgressState.
// The head maps this to its platform taskbar implementation.
public enum TaskbarProgressState
{
    None,
    Indeterminate,
    Normal,
    Error,
    Paused,
}
