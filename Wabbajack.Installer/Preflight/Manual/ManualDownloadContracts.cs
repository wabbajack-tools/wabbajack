using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Installer.Preflight;

public enum ManualDownloadState
{
    Pending,
    Detected,
    Waiting,
    Verifying,
    Moved,
    WrongFile,
    Failed
}

/// <summary>
///     One archive the user has to download by hand. <see cref="Key" /> is <c>Archive.Name</c>, compared
///     ordinal-ignore-case; two archives can share a hash under different names, so the hash is not a key.
/// </summary>
public sealed record ManualDownloadItem(string Key, Archive Archive, int Order, ManualDownloadState State,
    AbsolutePath? CandidatePath, AbsolutePath? PlacedPath, Percent Progress, string? Message);

public sealed record ManualDownloadCounts(int Total, int Moved, int Pending, int WrongFile, int Failed, int InProgress);

public sealed record ManualDownloadEvent(ManualDownloadItem Item, ManualDownloadState Previous, ManualDownloadCounts Counts);

public enum ManualDownloadNoticeKind
{
    WatchFolderUnavailable,
    WatchFolderChanged,
    DuplicateIgnored,
    CloudPlaceholder,
    WatcherError
}

public sealed record ManualDownloadNotice(ManualDownloadNoticeKind Kind, AbsolutePath? Path, string Message);

public sealed class ManualDownloadAcquirerOptions
{
    /// <summary>How often the watch folder is enumerated regardless of change notifications.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gap between size samples taken before a candidate is opened for verification.</summary>
    public TimeSpan StableInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Number of consecutive unchanged samples that count as stable.</summary>
    public int StableSamples { get; init; } = 2;

    /// <summary>First delay after the exclusive open finds the file still held by its writer.</summary>
    public TimeSpan LockRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound for the doubling lock retry delay.</summary>
    public TimeSpan LockRetryCap { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Use a <c>FileSystemWatcher</c> alongside polling.</summary>
    public bool UseFileSystemWatcher { get; init; } = true;

    /// <summary>Copy into place even when a rename would do; for tests and for folders where renames misbehave.</summary>
    public bool ForceCopyPlacement { get; init; } = false;

    /// <summary>Extensions that mark a file as still being written by a download tool.</summary>
    public IReadOnlySet<Extension> PartialExtensions { get; init; } = CandidateFile.DefaultPartialExtensions;
}

/// <summary>
///     Watches a folder for the files of a set of archives, verifies them and moves them into the downloads
///     folder. Thread-safe; events may be raised from any thread.
/// </summary>
public interface IManualDownloadAcquirer : IAsyncDisposable
{
    IObservable<ManualDownloadEvent> Events { get; }
    IObservable<ManualDownloadNotice> Notices { get; }

    /// <summary>All items in queue order.</summary>
    IReadOnlyList<ManualDownloadItem> Snapshot();

    /// <summary>The first item, by order, that has not been moved; null once everything is in place.</summary>
    ManualDownloadItem? Current { get; }

    ManualDownloadCounts Counts { get; }
    AbsolutePath WatchFolder { get; }
    AbsolutePath DestinationFolder { get; }

    Task Start(IEnumerable<Archive> pending, AbsolutePath watchFolder, AbsolutePath destinationFolder,
        CancellationToken token);

    void SetWatchFolder(AbsolutePath folder);

    /// <summary>Moves an item to the end of the queue.</summary>
    void Skip(string key);

    /// <summary>Returns a WrongFile or Failed item to the queue.</summary>
    void Retry(string key);

    /// <summary>Verifies a file the user picked, against every pending item.</summary>
    Task AddFileManually(string key, AbsolutePath file, CancellationToken token);

    Task Rescan(CancellationToken token);
    Task WaitForCompletion(CancellationToken token);
    Task Stop();
}
