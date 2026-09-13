using Wabbajack.DTOs;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     How a running check reports progress. Both methods may be called from any thread; the runner throttles
///     what it forwards to hosts so a check hashing thousands of files does not flood the UI.
/// </summary>
public interface IPreflightProgress
{
    void Report(long current, long total, string? text = null);

    void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null);
}
