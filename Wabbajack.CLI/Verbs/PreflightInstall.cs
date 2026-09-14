using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.CLI.Verbs;

/// <summary>
///     Runs the preflight checklist and, when every check passed, the installer. Shared by the verbs that
///     install a list. Automated downloads happen inside preflight; the CLI cannot wait for the user to
///     fetch files by hand, so whatever is left is printed and the run stops with
///     <see cref="PreflightFailed" />.
/// </summary>
public static class PreflightInstall
{
    public const int Succeeded = 0;
    public const int InstallFailed = 2;
    public const int PreflightFailed = 3;

    public static async Task<int> Run(IServiceProvider provider, InstallerConfiguration configuration,
        ILogger logger, CancellationToken token)
    {
        var runner = PreflightRunner.Create(provider, configuration,
            new PreflightOptions {WaitForManualDownloads = false});

        IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> manual =
            Array.Empty<(Archive, ManualDownloadTarget)>();
        var lastLogged = new Dictionary<string, (PreflightState State, string Message)>();

        runner.Changed += evt =>
        {
            switch (evt)
            {
                case CheckChanged {Status: var check}:
                    // Progress ticks arrive as CheckChanged too; only a new state or message is worth a line.
                    lock (lastLogged)
                    {
                        if (lastLogged.TryGetValue(check.Id, out var last) &&
                            last.State == check.State && last.Message == check.Message)
                            return;
                        lastLogged[check.Id] = (check.State, check.Message);
                    }

                    logger.LogInformation("[{State}] {Title}: {Message}", check.State, check.Title, check.Message);
                    break;
                case ArchiveChanged {Status: var archive} when archive.State is ArchiveState.Downloaded
                    or ArchiveState.ManualRequired or ArchiveState.Failed:
                    logger.LogInformation("[{State}] {Archive}: {Message}", archive.State, archive.Archive.Name,
                        archive.Message);
                    break;
                case ManualQueueChanged {Queue: var queue}:
                    manual = queue;
                    break;
            }
        };

        var outcome = await runner.RunAll(token);
        if (outcome.Ready)
        {
            var result = await StandardInstaller.Create(provider, configuration).Begin(token);
            return result == InstallResult.Succeeded ? Succeeded : InstallFailed;
        }

        // A run that stopped at a check the user has to act on leaves the rest Pending. Those have nothing
        // to report - they never ran - so only what actually produced a result is printed.
        foreach (var check in outcome.Checks.Where(c => !c.IsSatisfied && c.State != PreflightState.Pending))
        {
            logger.LogError("[{State}] {Title}: {Message}", check.State, check.Title, check.Message);
            if (!string.IsNullOrWhiteSpace(check.Detail))
                logger.LogError("{Detail}", check.Detail);
        }

        var notRun = outcome.Checks.Where(c => c.State == PreflightState.Pending).ToList();
        if (notRun.Count > 0)
            logger.LogError("Preflight stopped before {Count} further checks: {Checks}", notRun.Count,
                string.Join(", ", notRun.Select(c => c.Title)));

        if (manual.Count == 0)
            manual = runner.Context.State.ManualQueue.Select(q => (q.Archive, q.Target)).ToList();

        if (manual.Count > 0)
        {
            logger.LogError("{Count} files must be downloaded by hand into {Downloads} before installing:",
                manual.Count, configuration.Downloads);
            foreach (var (archive, target) in manual)
            {
                logger.LogError("{Name} ({Size})\n  {Url}\n  {Instructions}", archive.Name,
                    archive.Size.ToFileSizeString(), target.Url, target.Instructions);
            }
        }

        return PreflightFailed;
    }
}
