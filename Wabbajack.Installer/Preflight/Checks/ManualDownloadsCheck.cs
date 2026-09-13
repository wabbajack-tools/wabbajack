using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Walks the user through the archives no automated source could supply, and does so first: the part of
///     an install that needs the user's hands comes before the part they can walk away from. The acquirer
///     watches the Downloads folder, verifies what lands and moves it into place; this check publishes the
///     queue, waits for it when the host can (the GUI), re-verifies what was placed, and reports whatever is
///     still outstanding. A host that cannot wait (the CLI) gets the list straight away.
///     <para>
///         Running first makes this the check that usually computes the run's <see cref="DownloadPlan" />,
///         and the one that decides what the queue holds. automated-downloads reuses the plan and can push
///         archives back into the queue afterwards; re-running this check works those too.
///     </para>
/// </summary>
public sealed class ManualDownloadsCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.ManualDownloads;
    public string Title => "Manual downloads";
    public int Order => 600;

    public IReadOnlyList<string> DependsOn => new[]
    {
        PreflightCheckIds.ArchiveInventory, PreflightCheckIds.NexusLogin, PreflightCheckIds.UnsupportedArchives
    };

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        if (ctx.State.Missing.Count == 0 && ctx.State.ManualQueue.Count == 0)
            return PreflightResult.Passed("Nothing to download by hand");

        try
        {
            // The split is what fills the queue, so this check asks for the plan rather than reading it.
            await DownloadPlan.For(ctx, progress, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DownloadPlan.CouldNotLoad(ex);
        }

        // Computing the plan filled the queue with what the split sends to the browser; a later run finds
        // what the last one could not place, plus whatever automated-downloads pushed back into it.
        var pending = ctx.State.ManualQueue;
        if (pending.Count == 0)
            return PreflightResult.Passed("Nothing to download by hand");

        // A download can only be verified against a hash and matched by size; an archive without either
        // is reported rather than handed to the acquirer, which would refuse the whole queue.
        var unverifiable = pending.Where(q => q.Archive.Hash == default || q.Archive.Size <= 0).ToList();
        foreach (var item in unverifiable)
            progress.Archive(item.Archive, ArchiveState.Unsupported, UnverifiableReason(item.Archive));
        var queue = pending.Except(unverifiable).ToList();

        var byName = queue.ToDictionary(q => q.Archive.Name, q => q, StringComparer.OrdinalIgnoreCase);
        progress.ManualQueue(queue.Select(q => (q.Archive, q.Target)).ToList());
        foreach (var item in queue)
            progress.Archive(item.Archive, ArchiveState.ManualRequired, item.Reason);

        var acquirer = ctx.Acquirer;
        var watchFolder = ctx.Options.WatchFolder ?? KnownFolders.Downloads;
        await acquirer.Start(queue.Select(q => q.Archive), watchFolder, ctx.Config.Downloads, token);

        IReadOnlyList<ManualDownloadItem> snapshot;
        if (!ctx.Options.WaitForManualDownloads)
        {
            snapshot = acquirer.Snapshot();
            await acquirer.Stop();
        }
        else
        {
            progress.Report(acquirer.Counts.Moved, queue.Count, "Waiting for downloads");
            using var subscription = acquirer.Events.Subscribe(evt => Forward(evt, progress));
            try
            {
                await acquirer.WaitForCompletion(token);
            }
            finally
            {
                await acquirer.Stop();
            }

            snapshot = acquirer.Snapshot();
        }

        var placed = 0;
        var outstanding = new List<ManualQueueItem>();
        foreach (var item in snapshot)
        {
            var queued = byName[item.Key];
            if (item.State == ManualDownloadState.Moved && await Verified(ctx, item, queued, token))
            {
                ctx.State.HashedArchives[queued.Archive.Name] = item.PlacedPath!.Value;
                placed++;
                progress.Archive(queued.Archive, ArchiveState.Present, item.PlacedPath.ToString());
                continue;
            }

            outstanding.Add(queued);
            if (item.State == ManualDownloadState.Moved)
                progress.Archive(queued.Archive, ArchiveState.Failed,
                    "The file put in place does not match what the list expects");
        }

        // What was placed leaves the queue for good; what is still outstanding, and what could never be
        // verified, stays in it, so a later run of this check picks up exactly what is left.
        ctx.State.ManualQueue = pending.Where(q => !ctx.State.HashedArchives.ContainsKey(q.Archive.Name)).ToList();
        ctx.State.Missing = ctx.State.Missing.Where(a => !ctx.State.HashedArchives.ContainsKey(a.Name)).ToList();
        ctx.State.RemainingDownloadBytes = ctx.State.Missing.Sum(a => a.Size);

        if (outstanding.Count == 0 && unverifiable.Count == 0)
            return PreflightResult.Passed($"{Plural.Of(placed, "file")} downloaded by hand and verified");

        var detail = ManualQueueItem.Describe(outstanding);
        if (unverifiable.Count > 0)
        {
            var lines = unverifiable.Select(u => $"{u.Archive.Name} - {UnverifiableReason(u.Archive)}");
            detail = string.Join(Environment.NewLine, new[] {detail}.Concat(lines).Where(s => s.Length > 0));
        }

        if (outstanding.Count == 0)
            return PreflightResult.Failed(
                Plural.Of(unverifiable.Count, "file cannot be verified after downloading and is unsupported",
                    "files cannot be verified after downloading and are unsupported"), detail);

        var size = outstanding.Sum(o => o.Archive.Size).ToFileSizeString();
        var message = $"{Plural.Of(outstanding.Count, "file")} must be downloaded by hand ({size})";
        if (unverifiable.Count > 0)
            message += "; " + Plural.Of(unverifiable.Count, "cannot be verified and is unsupported",
                "cannot be verified and are unsupported");
        return PreflightResult.NeedsUser(message, detail, new[] {PreflightAction.Rescan});
    }

    private static string UnverifiableReason(Archive archive)
    {
        return archive.Hash == default
            ? "The list gives no hash for this file, so a download of it could never be verified"
            : "The list gives no size for this file, so a download of it could never be matched";
    }

    private static async Task<bool> Verified(PreflightContext ctx, ManualDownloadItem item, ManualQueueItem queued,
        CancellationToken token)
    {
        if (item.PlacedPath is not { } path || !path.FileExists()) return false;
        return await ctx.HashCache.FileHashCachedAsync(path, token) == queued.Archive.Hash;
    }

    private static void Forward(ManualDownloadEvent evt, IPreflightProgress progress)
    {
        var item = evt.Item;
        var state = item.State switch
        {
            ManualDownloadState.Detected or ManualDownloadState.Waiting or ManualDownloadState.Verifying =>
                ArchiveState.ManualInProgress,
            ManualDownloadState.Moved => ArchiveState.Present,
            ManualDownloadState.WrongFile or ManualDownloadState.Failed => ArchiveState.Failed,
            _ => ArchiveState.ManualRequired
        };
        long? bytes = item.State == ManualDownloadState.Verifying
            ? (long) (item.Progress.Value * item.Archive.Size)
            : null;
        progress.Archive(item.Archive, state, item.Message ?? Describe(item.State), bytes);
        progress.Report(evt.Counts.Moved, evt.Counts.Total, $"{evt.Counts.Moved} of {evt.Counts.Total} in place");
    }

    private static string Describe(ManualDownloadState state)
    {
        return state switch
        {
            ManualDownloadState.Detected => "Found, waiting for the download to finish",
            ManualDownloadState.Waiting => "Your browser is still writing this file",
            ManualDownloadState.Verifying => "Verifying",
            _ => state.ToString()
        };
    }
}
