namespace Wabbajack.Installer.Preflight;

public enum PreflightState
{
    Pending,
    Running,
    Passed,
    Warning,
    Failed,
    NeedsUser,
    Skipped,
    Cancelled
}
