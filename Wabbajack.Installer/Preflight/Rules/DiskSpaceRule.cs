using System;
using Wabbajack.Common;

namespace Wabbajack.Installer.Preflight.Rules;

public enum DiskSpaceLevel
{
    Ok,
    Warn,
    Block
}

/// <summary>
///     Free space is measured per folder root, separately for the install and downloads folders, because
///     the two commonly sit on different drives.
/// </summary>
public record DiskSpaceInput(string InstallRoot, long InstallFree, string DownloadsRoot, long DownloadsFree,
    long InstallBytes, long RemainingDownloadBytes);

public record DiskSpaceVerdict(DiskSpaceLevel Level, string Message);

/// <summary>
///     Pure. Blocks only on what is known exactly: the bytes of archives still to be downloaded. The full
///     install size is only a warning, because updating an existing install needs far less than it.
/// </summary>
public static class DiskSpaceRule
{
    public static DiskSpaceVerdict Evaluate(DiskSpaceInput input)
    {
        var sameRoot = string.Equals(input.InstallRoot, input.DownloadsRoot, StringComparison.OrdinalIgnoreCase);

        if (input.RemainingDownloadBytes > 0 && input.DownloadsFree < input.RemainingDownloadBytes)
        {
            return new DiskSpaceVerdict(DiskSpaceLevel.Block,
                $"Not enough space for downloads: {input.RemainingDownloadBytes.ToFileSizeString()} still to download " +
                $"but only {input.DownloadsFree.ToFileSizeString()} free on {input.DownloadsRoot}");
        }

        var installNeeded = input.InstallBytes + (sameRoot ? input.RemainingDownloadBytes : 0);
        if (input.InstallFree < installNeeded)
        {
            var what = sameRoot && input.RemainingDownloadBytes > 0
                ? "A full install plus the remaining downloads need"
                : "A full install needs";
            return new DiskSpaceVerdict(DiskSpaceLevel.Warn,
                $"{what} {installNeeded.ToFileSizeString()} but only {input.InstallFree.ToFileSizeString()} is free on " +
                $"{input.InstallRoot}. Updating an existing install needs far less; continue if you know you have room.");
        }

        var message = sameRoot
            ? $"{input.InstallFree.ToFileSizeString()} free on {input.InstallRoot}"
            : $"{input.InstallFree.ToFileSizeString()} free on {input.InstallRoot}, " +
              $"{input.DownloadsFree.ToFileSizeString()} free on {input.DownloadsRoot}";
        return new DiskSpaceVerdict(DiskSpaceLevel.Ok, message);
    }
}
