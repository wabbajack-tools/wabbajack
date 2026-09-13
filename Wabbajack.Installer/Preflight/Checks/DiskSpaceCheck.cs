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
            var root = Path.GetPathRoot(folder.ToString());
            if (string.IsNullOrEmpty(root)) return null;
            return (root, new DriveInfo(root).AvailableFreeSpace);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read free space for {Folder}", folder);
            return null;
        }
    }
}
