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
    private DownloadPlan? _plan;

    /// <summary>
    ///     Set by nexus-login. Null when the list has no Nexus archives, in which case the download plan
    ///     fills it in if a mirror reroute introduces one.
    /// </summary>
    public NexusLoginStatus? Nexus { get; set; }

    /// <summary>Set by game-installed: the folder the primary game lives in.</summary>
    public AbsolutePath GameFolder { get; set; }

    /// <summary>Set by game-installed: every <c>OtherGames</c> entry that could be located.</summary>
    public Dictionary<Game, AbsolutePath> OtherGameFolders { get; } = new();

    /// <summary>
    ///     Set by archive-inventory: the archives this install will actually read, after pruning what an
    ///     existing install already has (see <c>RequiredArchives.Compute</c>). Writing it throws away the
    ///     download plan, which was partitioned from the previous answer.
    /// </summary>
    public Archive[] RequiredArchives
    {
        get => _requiredArchives;
        set
        {
            _requiredArchives = value;
            _plan = null;
        }
    }

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
}
