namespace Wabbajack.Installer.Preflight;

public sealed class PreflightOptions
{
    /// <summary>
    ///     When false (the CLI), a check that would otherwise sit waiting for the user to fetch files by hand
    ///     reports NeedsUser immediately instead.
    /// </summary>
    public bool WaitForManualDownloads { get; init; } = true;

    public bool SendMetrics { get; init; } = true;
}
