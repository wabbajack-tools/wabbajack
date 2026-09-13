using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     One archive the user has to fetch by hand, where to send them, and why it ended up here.
/// </summary>
public sealed record ManualQueueItem(Archive Archive, ManualDownloadTarget Target, string Reason);

/// <summary>
///     Facts that earlier checks establish and later checks read. Owned by one <see cref="PreflightContext" />,
///     so one preflight run at a time writes here.
/// </summary>
public sealed class PreflightBlackboard
{
    /// <summary>Set by nexus-login. Null when the list has no Nexus archives.</summary>
    public NexusLoginStatus? Nexus { get; set; }

    /// <summary>Set by game-installed: the folder the primary game lives in.</summary>
    public AbsolutePath GameFolder { get; set; }

    /// <summary>Set by game-installed: every <c>OtherGames</c> entry that could be located.</summary>
    public Dictionary<Game, AbsolutePath> OtherGameFolders { get; } = new();

    /// <summary>
    ///     Set by archive-inventory: the archives this install will actually read, after pruning what an
    ///     existing install already has (see <c>RequiredArchives.Compute</c>).
    /// </summary>
    public Archive[] RequiredArchives { get; set; } = Array.Empty<Archive>();

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
    ///     Set by automated-downloads: what is left for the user to fetch by hand, in modlist order.
    ///     manual-downloads hands it to the acquirer.
    /// </summary>
    public List<ManualQueueItem> ManualQueue { get; set; } = new();

    /// <summary>Bytes of archives still to be fetched; disk-space blocks on this.</summary>
    public long RemainingDownloadBytes { get; set; }
}
