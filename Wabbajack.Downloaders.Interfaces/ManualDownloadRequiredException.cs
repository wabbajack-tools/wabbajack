using System;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.Interfaces;

/// <summary>
///     Thrown by a downloader that cannot fetch its archive without a user and a browser.
///     <see cref="Target" /> is the page to open when one is known.
/// </summary>
public class ManualDownloadRequiredException : Exception
{
    public ManualDownloadRequiredException(Archive archive, ManualDownloadTarget? target, string reason)
        : base(BuildMessage(archive, target, reason))
    {
        Archive = archive;
        Target = target;
        Reason = reason;
    }

    public Archive Archive { get; }
    public ManualDownloadTarget? Target { get; }
    public string Reason { get; }

    private static string BuildMessage(Archive archive, ManualDownloadTarget? target, string reason)
    {
        return target == null
            ? $"{archive.Name} must be downloaded manually: {reason}"
            : $"{archive.Name} must be downloaded manually from {target.Url}: {reason}";
    }
}
