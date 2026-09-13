using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     How the missing archives divide between the automated downloaders and the user's browser, together
///     with the policy they were divided under. Both download checks read it and neither owns it: it is
///     computed by whichever of them asks first (manual-downloads, in the order they run) and memoised on
///     the blackboard, because nothing in it can safely be done twice. The mirror reroute rewrites the
///     archive states it touches, the Nexus probe is a network call, and a second split under a different
///     answer would move archives between the two checks halfway through a run.
///     <para>
///         The split by download state is only half the answer: an archive whose state is automated still
///         needs a downloader that can be prepared and a URL the allow-list permits, and neither takes a
///         download to establish. <see cref="ArchiveDownloadPipeline.Screen" /> settles both here, so an
///         account the user has no usable login for sends its archives to the browser at plan time instead
///         of part-way through the automated pass.
///     </para>
///     <para>
///         Computing the plan also fills the manual queue with everything it means to send to the browser,
///         so the queue is right whichever check asked for the plan.
///     </para>
/// </summary>
public sealed record DownloadPlan(
    ArchiveDownloadPipeline.DownloadPolicy Policy,
    IReadOnlyList<Archive> Missing,
    IReadOnlyList<Archive> Automated,
    IReadOnlyList<ManualQueueItem> Manual,
    IReadOnlyList<Archive> Unsupported,
    IReadOnlyList<(Archive Archive, string Reason)> Blocked)
{
    /// <summary>
    ///     Modlist position of every archive the plan partitioned, keyed by <c>Archive.Name</c>, so the
    ///     manual queue keeps that order however late something is added to it.
    /// </summary>
    public IReadOnlyDictionary<string, int> Order { get; } = Missing
        .Select((a, i) => (a.Name, Index: i))
        .ToDictionary(p => p.Name, p => p.Index, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The plan for this run, computed on the first call and handed back unchanged after. Only the
    ///     policy load can realistically throw; the Nexus probe reports its own failure by leaving the
    ///     account unknown, which sends the archives concerned to the manual queue.
    /// </summary>
    public static Task<DownloadPlan> For(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        return ctx.State.Plan(t => Compute(ctx, progress, t), token);
    }

    /// <summary>What a check reports when the plan could not be computed. Retrying reruns the whole load.</summary>
    public static PreflightResult CouldNotLoad(Exception ex)
    {
        return PreflightResult.Failed(
            $"Could not load the download rules from the Wabbajack server: {ex.Message}", ex.ToString(),
            new[] {PreflightAction.Retry}, ex);
    }

    /// <summary>
    ///     Puts <paramref name="items" /> on the run's manual queue in modlist order, skipping archives
    ///     already queued or already in hand. The split fills the queue this way when the plan is computed,
    ///     and automated-downloads adds to it the same way when a download turns out to need a browser.
    /// </summary>
    public void Enqueue(PreflightContext ctx, IEnumerable<ManualQueueItem> items)
    {
        var queued = new HashSet<string>(ctx.State.ManualQueue.Select(q => q.Archive.Name),
            StringComparer.OrdinalIgnoreCase);
        var added = items
            .Where(m => queued.Add(m.Archive.Name) && !ctx.State.HashedArchives.ContainsKey(m.Archive.Name))
            .ToList();
        if (added.Count == 0) return;

        ctx.State.ManualQueue = ctx.State.ManualQueue.Concat(added)
            .OrderBy(m => Order.TryGetValue(m.Archive.Name, out var index) ? index : int.MaxValue)
            .ToList();
    }

    private static async Task<DownloadPlan> Compute(PreflightContext ctx, IPreflightProgress progress,
        CancellationToken token)
    {
        var missing = ctx.State.Missing.ToList();
        var pipeline = new ArchiveDownloadPipeline(ctx, progress);

        progress.Report(0, 0, "Loading download rules");
        var policy = await pipeline.LoadPolicy(token);

        foreach (var archive in ArchiveDownloadPipeline.Reroute(missing, policy.Mirrors, ctx.Logger))
            pipeline.SendMetric("rerouted", archive.Hash.ToString());

        var premium = await pipeline.NexusPremium(missing, token);
        var split = ArchiveDownloadPipeline.Split(missing, premium);

        foreach (var item in split.Manual)
            progress.Archive(item.Archive, ArchiveState.ManualRequired, item.Reason);

        // unsupported-archives normally removes these long before now, so this is the last word rather than
        // the first: neither a downloader nor a browser can reach them.
        foreach (var archive in split.Unsupported)
            progress.Archive(archive, ArchiveState.Unsupported,
                $"{archive.State.GetType().Name} source, nothing can download it");

        // Screening reports what it turns away itself, in the same terms the download pass would have.
        var screening = await pipeline.Screen(split.Automated, policy);
        var manual = split.Manual.Concat(screening.Manual).ToList();

        var plan = new DownloadPlan(policy, missing, screening.Ready, manual, split.Unsupported,
            screening.Blocked);
        plan.Enqueue(ctx, manual);
        return plan;
    }
}
