using System;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders;

/// <summary>
///     A download gave up because the transfer stalled, not because the user cancelled it.
///     Distinguished from <see cref="OperationCanceledException" /> so callers can retry.
/// </summary>
public class DownloadTimeoutException : Exception
{
    public DownloadTimeoutException(Archive archive, Exception inner)
        : base($"Timed out downloading {archive.Name}", inner)
    {
        Archive = archive;
    }

    public Archive Archive { get; }
}
