using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Installer.Preflight;

public enum ArchiveState
{
    Unknown,
    Present,
    Missing,
    Downloading,
    Downloaded,
    ManualRequired,
    ManualInProgress,
    Unsupported,
    Failed
}

/// <summary>
///     What preflight currently knows about one archive. Identified by <c>Archive.Name</c> (compared
///     OrdinalIgnoreCase), never by hash: two archives can share bytes under different names.
/// </summary>
public record ArchiveStatus(Archive Archive, ArchiveState State, string? Message, long? Progress,
    ManualDownloadTarget? Target);
