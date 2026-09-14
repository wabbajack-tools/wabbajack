using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     One archive the user has to fetch by hand, where to send them, and why it ended up here.
/// </summary>
public sealed record ManualQueueItem(Archive Archive, ManualDownloadTarget Target, string Reason)
{
    /// <summary>One block per file: what it is, where to get it, what to do there. Printable as-is by the CLI.</summary>
    public static string Describe(IEnumerable<ManualQueueItem> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append(item.Archive.Name).Append(" (").Append(item.Archive.Size.ToFileSizeString()).Append(") - ")
                .Append(item.Target.SiteName).Append(": ").Append(item.Target.Url).AppendLine();
            sb.Append("    ").AppendLine(item.Target.Instructions);
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
///     Facts that earlier checks establish and later checks read. Owned by one <see cref="PreflightContext" />,
///     so one preflight run at a time writes here.
/// </summary>
public sealed class PreflightBlackboard
{
    private readonly SemaphoreSlim _planLock = new(1, 1);
    private Archive[] _requiredArchives = Array.Empty<Archive>();
    private NexusLoginStatus? _nexus;
    /// <summary>Volatile because the fast path below reads it without taking <see cref="_planLock" />.</summary>
    private volatile DownloadPlan? _plan;

    /// <summary>
    ///     Set by nexus-login. Null when nothing this install still has to fetch comes from Nexus Mods, in
    ///     which case the download plan fills it in if a mirror reroute introduces one.
    ///     <para>
    ///         The plan is partitioned partly on this answer, so changing it throws the plan away, exactly as
    ///         writing <see cref="RequiredArchives" /> does. Without that, a free account that logged in again
    ///         as premium kept the split made while it was free: the row turned green and manual-downloads,
    ///         re-reading the memo, still demanded by hand the files the app could now fetch itself. Only the
    ///         premium answer moves an archive between the two sides - a Nexus archive is automated when the
    ///         account is premium and goes to the browser otherwise - so that is what is compared, and an
    ///         unknown account counts as not premium, which is how it is partitioned. Re-probing the same
    ///         account therefore costs nothing, while the login that changes something starts a fresh plan.
    ///     </para>
    /// </summary>
    public NexusLoginStatus? Nexus
    {
        get => _nexus;
        set
        {
            var repartitions = _nexus?.IsPremium == true != (value?.IsPremium == true);
            _nexus = value;
            // Only when there is a plan to drop: the plan's own probe writes here while it is being
            // computed, and the queue it is in the middle of filling is not stale work.
            if (repartitions && _plan != null) DropPlan();
        }
    }

    /// <summary>Set by game-installed: the folder the primary game lives in.</summary>
    public AbsolutePath GameFolder { get; set; }

    /// <summary>Set by game-installed: every <c>OtherGames</c> entry that could be located.</summary>
    public Dictionary<Game, AbsolutePath> OtherGameFolders { get; } = new();

    /// <summary>
    ///     Set by archive-inventory: the archives this install will actually read, after pruning what an
    ///     existing install already has (see <c>RequiredArchives.Compute</c>). Writing it throws the download
    ///     plan away, which was partitioned from the previous answer - see <see cref="DropPlan" />.
    /// </summary>
    public Archive[] RequiredArchives
    {
        get => _requiredArchives;
        set
        {
            _requiredArchives = value;
            DropPlan();
        }
    }

    /// <summary>
    ///     Archives a mirror reroute pointed somewhere else, by <c>Archive.Name</c>. The reroute rewrites the
    ///     modlist's own <c>Archive.State</c>, so a state the list never carried - a Nexus one, typically -
    ///     is indistinguishable from one it did by the time any later pass looks. nexus-login reads this to
    ///     tell them apart: a reroute-introduced Nexus download without a login belongs on the manual queue,
    ///     which is what <c>ArchiveDownloadPipeline.NexusPremium</c> arranges, and must not halt the run on a
    ///     second pass over the same list. It survives the plan being dropped because the rewritten states do.
    /// </summary>
    public HashSet<string> Rerouted { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Set by archive-inventory: where each required archive was found on disk, keyed by
    ///     <c>Archive.Name</c> (OrdinalIgnoreCase). Keyed by name rather than hash for the same reason the
    ///     runner's archive snapshot is: two archives can share bytes under different names.
    /// </summary>
    public ConcurrentDictionary<string, AbsolutePath> HashedArchives { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Required archives not found on disk, excluding game files (those are game-files' business). Later
    ///     checks remove what they resolve or rule out.
    /// </summary>
    public List<Archive> Missing { get; set; } = new();

    /// <summary>
    ///     What is left for the user to fetch by hand, in modlist order. manual-downloads rebuilds it from
    ///     the download plan and hands it to the acquirer; automated-downloads appends whatever its pass
    ///     turned out to need a browser after all.
    /// </summary>
    public List<ManualQueueItem> ManualQueue { get; set; } = new();

    /// <summary>Bytes of archives still to be fetched; disk-space blocks on this.</summary>
    public long RemainingDownloadBytes { get; set; }

    /// <summary>
    ///     The run's download plan, computed by <paramref name="compute" /> the first time it is asked for
    ///     and reused after: both download checks partition the same way, and the reroute and the Nexus
    ///     probe behind it are not repeatable. A failed computation is not remembered, so a retry recomputes.
    /// </summary>
    public async Task<DownloadPlan> Plan(Func<CancellationToken, Task<DownloadPlan>> compute,
        CancellationToken token)
    {
        if (_plan != null) return _plan;

        await _planLock.WaitAsync(token);
        try
        {
            return _plan ??= await compute(token);
        }
        finally
        {
            _planLock.Release();
        }
    }

    /// <summary>
    ///     Forgets the memoised plan, and the manual queue with it: the queue is what that plan sent to the
    ///     browser, so keeping it would ask the user for files the next plan may not need. Whatever is still
    ///     genuinely missing is partitioned again when a download check asks for a plan.
    /// </summary>
    private void DropPlan()
    {
        _plan = null;
        ManualQueue = new List<ManualQueueItem>();
    }
}
