using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Runs last so it sees what the download checks left to fetch. Free space is read per folder root,
///     for the install and downloads folders separately.
/// </summary>
public sealed class DiskSpaceCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.DiskSpace;
    public string Title => "Disk space";
    public int Order => 900;
    public IReadOnlyList<string> DependsOn => new[] {PreflightCheckIds.ArchiveInventory};

    public Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var installBytes = ctx.Metadata?.DownloadMetadata?.SizeOfInstalledFiles
                           ?? ctx.ModList.Directives.Sum(d => d.Size);
        var remaining = ctx.State.RemainingDownloadBytes;

        var install = FreeSpace(ctx.Config.Install, ctx.Logger);
        var downloads = FreeSpace(ctx.Config.Downloads, ctx.Logger);
        if (install == null || downloads == null)
            return Task.FromResult(PreflightResult.Passed("Could not determine free space, skipping the check"));

        var verdict = DiskSpaceRule.Evaluate(new DiskSpaceInput(install.Value.Root, install.Value.Free,
            downloads.Value.Root, downloads.Value.Free, installBytes, remaining));

        return Task.FromResult(verdict.Level switch
        {
            DiskSpaceLevel.Block => PreflightResult.Failed(verdict.Message, actions: new[] {PreflightAction.Retry}),
            DiskSpaceLevel.Warn => PreflightResult.Warning(verdict.Message,
                actions: new[] {PreflightAction.ContinueAnyway}),
            _ => PreflightResult.Passed(verdict.Message)
        });
    }

    private static (string Root, long Free)? FreeSpace(AbsolutePath folder, ILogger logger)
    {
        try
        {
            return FreeSpaceOf(folder);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read free space for {Folder}", folder);
            return null;
        }
    }

    /// <summary>The drive or mount point a folder is on and the space free there; null when there is none.</summary>
    public static (string Root, long Free)? FreeSpaceOf(AbsolutePath folder)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Every Unix path has the root "/", which says nothing about which file system it is on: the
            // install's drive is the mount point the folder is under, the deepest one when they nest.
            var drives = DriveInfo.GetDrives();
            var mount = MountPointOf(folder.ToString(), drives.Select(d => d.RootDirectory.FullName));
            if (mount == null) return null;
            return (mount, drives.First(d => d.RootDirectory.FullName == mount).AvailableFreeSpace);
        }

        var root = Path.GetPathRoot(folder.ToString());
        if (string.IsNullOrEmpty(root)) return null;
        return (root, new DriveInfo(root).AvailableFreeSpace);
    }

    /// <summary>The deepest mount point a Unix path is under, or null when none of them holds it.</summary>
    public static string? MountPointOf(string path, IEnumerable<string> mountPoints)
    {
        return mountPoints
            .Where(m => m == "/" || path == m.TrimEnd('/') || path.StartsWith(m.TrimEnd('/') + "/", StringComparison.Ordinal))
            .OrderByDescending(m => m.Length)
            .FirstOrDefault();
    }
}
