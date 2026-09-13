using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight.Rules;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Fetches every missing archive an automated source can supply (the Wabbajack CDN, direct links, and
///     Nexus Mods for premium accounts) and hands the rest to manual-downloads. A download that cannot be
///     completed is never fatal here: the user can still fetch it by hand.
/// </summary>
public sealed class AutomatedDownloadsCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.AutomatedDownloads;
    public string Title => "Automated downloads";
    public int Order => 600;

    public IReadOnlyList<string> DependsOn => new[]
    {
        PreflightCheckIds.ArchiveInventory, PreflightCheckIds.NexusLogin, PreflightCheckIds.UnsupportedArchives
    };

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        ctx.State.ManualQueue = new List<ManualQueueItem>();
        var missing = ctx.State.Missing.ToList();
        if (missing.Count == 0)
        {
            ctx.State.RemainingDownloadBytes = 0;
            return PreflightResult.Passed("Nothing to download");
        }

        var pipeline = new ArchiveDownloadPipeline(ctx, progress);

        ArchiveDownloadPipeline.DownloadPolicy policy;
        try
        {
            progress.Report(0, 0, "Loading download rules");
            policy = await pipeline.LoadPolicy(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PreflightResult.Failed(
                $"Could not load the download rules from the Wabbajack server: {ex.Message}", ex.ToString(),
                new[] {PreflightAction.Retry}, ex);
        }

        foreach (var archive in ArchiveDownloadPipeline.Reroute(missing, policy.Mirrors, ctx.Logger))
            pipeline.SendMetric("rerouted", archive.Hash.ToString());

        var premium = await pipeline.NexusPremium(missing, token);
        var split = ArchiveDownloadPipeline.Split(missing, premium);
        foreach (var item in split.Manual)
            progress.Archive(item.Archive, ArchiveState.ManualRequired, item.Reason);
        foreach (var archive in split.Unsupported)
            progress.Archive(archive, ArchiveState.Unsupported,
                $"{archive.State.GetType().Name} source, nothing can download it");

        var outcome = await pipeline.Download(split.Automated, policy, token);

        var order = missing.Select((a, i) => (a.Name, i))
            .ToDictionary(p => p.Name, p => p.i, StringComparer.OrdinalIgnoreCase);
        ctx.State.ManualQueue = split.Manual.Concat(outcome.Manual)
            .OrderBy(m => order[m.Archive.Name])
            .ToList();
        ctx.State.Missing = missing.Where(a => !ctx.State.HashedArchives.ContainsKey(a.Name)).ToList();
        ctx.State.RemainingDownloadBytes = ctx.State.Missing.Sum(a => a.Size);

        var manual = ctx.State.ManualQueue.Count;
        var message = manual == 0
            ? $"{outcome.Downloaded.Count} downloaded"
            : $"{outcome.Downloaded.Count} downloaded, {manual} moved to manual " +
              $"({ctx.State.ManualQueue.Sum(m => m.Archive.Size).ToFileSizeString()})";

        var failed = outcome.Failed
            .Concat(split.Unsupported.Select(a => (Archive: a, Reason: "no downloader and no page to send you to")))
            .ToList();
        if (failed.Count == 0)
            return PreflightResult.Passed(message);

        var detail = string.Join(Environment.NewLine, failed.Select(f => $"{f.Archive.Name}: {f.Reason}"));
        return PreflightResult.Failed($"{message}, {failed.Count} could not be fetched at all", detail,
            new[] {PreflightAction.Retry});
    }
}
