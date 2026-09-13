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
///     Nexus Mods for premium accounts). It runs after manual-downloads so the user can leave the machine to
///     it, and takes the split it works from off the run's <see cref="DownloadPlan" />, which manual-downloads
///     normally computed.
///     <para>
///         A download that cannot be completed is never fatal here: the user can still fetch it by hand. It
///         does mean the queue manual-downloads already emptied has filled up again, though, so the check
///         ends needing the user, offering to send them back to manual-downloads. Nothing else would notice:
///         every other check has either run or does not care, and the run would otherwise be called ready
///         with archives nobody has fetched. That is for a download that only turns out to need a browser
///         once it is running - a stall, a refusal, a page the downloader lands on. What was knowable before
///         a byte moved, such as a source with no usable login, the plan's screening has already sent to
///         manual-downloads.
///     </para>
/// </summary>
public sealed class AutomatedDownloadsCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.AutomatedDownloads;
    public string Title => "Automated downloads";
    public int Order => 700;
    public IReadOnlyList<string> DependsOn => new[] {PreflightCheckIds.ManualDownloads};

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        if (ctx.State.Missing.Count == 0)
        {
            ctx.State.RemainingDownloadBytes = 0;
            return PreflightResult.Passed("Nothing to download");
        }

        DownloadPlan plan;
        try
        {
            plan = await DownloadPlan.For(ctx, progress, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DownloadPlan.CouldNotLoad(ex);
        }

        // Anything already in hand, and anything already waiting on the user, is not downloaded again: a
        // second pass over an archive the first one sent to the manual queue would only send it there again.
        var queued = new HashSet<string>(ctx.State.ManualQueue.Select(m => m.Archive.Name),
            StringComparer.OrdinalIgnoreCase);
        var pending = plan.Automated
            .Where(a => !ctx.State.HashedArchives.ContainsKey(a.Name) && !queued.Contains(a.Name))
            .ToList();

        var outcome = await new ArchiveDownloadPipeline(ctx, progress).Download(pending, token);

        if (outcome.Manual.Count > 0)
        {
            plan.Enqueue(ctx, outcome.Manual);
            progress.ManualQueue(ctx.State.ManualQueue.Select(m => (m.Archive, m.Target)).ToList());
        }

        ctx.State.Missing = ctx.State.Missing.Where(a => !ctx.State.HashedArchives.ContainsKey(a.Name)).ToList();
        ctx.State.RemainingDownloadBytes = ctx.State.Missing.Sum(a => a.Size);

        var manual = ctx.State.ManualQueue;
        var message = manual.Count == 0
            ? $"{outcome.Downloaded.Count} downloaded"
            : $"{outcome.Downloaded.Count} downloaded, {Plural.Of(manual.Count, "file")} still to fetch by hand " +
              $"({manual.Sum(m => m.Archive.Size).ToFileSizeString()})";

        var failed = outcome.Failed
            .Concat(plan.Blocked)
            .Concat(plan.Unsupported.Select(a => (Archive: a, Reason: "no downloader and no page to send you to")))
            .ToList();
        if (failed.Count > 0)
        {
            var detail = string.Join(Environment.NewLine, failed.Select(f => $"{f.Archive.Name}: {f.Reason}"));
            return PreflightResult.Failed($"{message}, {failed.Count} could not be fetched at all", detail,
                new[] {PreflightAction.Retry});
        }

        // The queue is empty whenever manual-downloads passed and this pass added nothing, so anything left
        // in it is work the user has not been walked through yet.
        if (manual.Count == 0)
            return PreflightResult.Passed(message);

        return PreflightResult.NeedsUser(message, ManualQueueItem.Describe(manual),
            new[] {PreflightAction.DownloadByHand});
    }
}
