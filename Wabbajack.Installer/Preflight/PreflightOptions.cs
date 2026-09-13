using System;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight;

public sealed class PreflightOptions
{
    /// <summary>
    ///     When false (the CLI), a check that would otherwise sit waiting for the user to fetch files by hand
    ///     reports NeedsUser immediately instead.
    /// </summary>
    public bool WaitForManualDownloads { get; init; } = true;

    public bool SendMetrics { get; init; } = true;

    /// <summary>
    ///     The folder watched for files the user downloads by hand. Null means the user's Downloads folder.
    /// </summary>
    public AbsolutePath? WatchFolder { get; init; }

    /// <summary>
    ///     First pause before a stalled or refused automated download is tried again; doubles per attempt.
    /// </summary>
    public TimeSpan DownloadRetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}
