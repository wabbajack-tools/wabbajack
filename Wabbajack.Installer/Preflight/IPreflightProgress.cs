using System.Collections.Generic;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     How a running check reports progress. All methods may be called from any thread; the runner throttles
///     what it forwards to hosts so a check hashing thousands of files does not flood the UI.
/// </summary>
public interface IPreflightProgress
{
    void Report(long current, long total, string? text = null);

    void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null);

    /// <summary>
    ///     Publishes the archives the user has to fetch by hand, with the page to open for each. The runner
    ///     records the target against each archive and raises <see cref="ManualQueueChanged" />.
    /// </summary>
    void ManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue);
}
