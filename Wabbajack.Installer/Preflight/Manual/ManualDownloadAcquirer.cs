using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Watches a folder (normally the user's Downloads folder) for the archives of a modlist that have to be
///     fetched by hand, verifies each one and moves it into the downloads folder of the install.
///     <para>
///         Detection is a <see cref="FileSystemWatcher" /> plus a periodic poll; the signal that a download has
///         finished is an exclusive open succeeding, not the size, because several download tools reserve the
///         final size up front. A candidate is matched against every pending archive of its size, then by
///         hash, so a file for the fortieth item landing while the third is on screen simply completes the
///         fortieth. The user's files are never deleted, other than a source that has been copied across
///         volumes and verified.
///     </para>
/// </summary>
public sealed class ManualDownloadAcquirer : IManualDownloadAcquirer, IDisposable
{
    /// <summary>Floor for the polling delays; a zero interval would turn the wait loops into busy spins.</summary>
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromMilliseconds(10);

    private sealed class Entry
    {
        public required string Key { get; init; }
        public required Archive Archive { get; init; }
        public int Order { get; set; }
        public ManualDownloadState State { get; set; } = ManualDownloadState.Pending;
        public AbsolutePath? CandidatePath { get; set; }
        public AbsolutePath? PlacedPath { get; set; }
        public Percent Progress { get; set; } = Percent.Zero;
        public string? Message { get; set; }

        /// <summary>A placement is running for this entry; no other candidate may claim it.</summary>
        public bool Busy { get; set; }

        public ManualDownloadItem ToItem()
        {
            return new ManualDownloadItem(Key, Archive, Order, State, CandidatePath, PlacedPath, Progress, Message);
        }
    }

    private readonly ILogger<ManualDownloadAcquirer> _logger;
    private readonly FileHashCache _hashCache;
    private readonly IResource<FileHashCache> _hashLimiter;
    private readonly DownloadDispatcher _dispatcher;
    private readonly ManualDownloadAcquirerOptions _options;
    private readonly TimeSpan _stableInterval;
    private readonly TimeSpan _lockRetryDelay;
    private readonly TimeSpan _lockRetryCap;

    private readonly object _gate = new();
    private readonly object _publishGate = new();
    private readonly Subject<ManualDownloadEvent> _events = new();
    private readonly Subject<ManualDownloadNotice> _notices = new();
    private bool _disposed;

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<long, Entry[]> _bySize = new();
    /// <summary>Sizes with at least one item not yet in place; what the poll filter looks up.</summary>
    private HashSet<long> _openSizes = new();
    private readonly Dictionary<AbsolutePath, (long Size, DateTime LastWrite, Hash Hash)> _seen = new();
    private readonly HashSet<AbsolutePath> _noticed = new();
    /// <summary>Paths being evaluated; the value records that the path was reported again meanwhile.</summary>
    private readonly Dictionary<AbsolutePath, bool> _inFlight = new();
    private readonly ConcurrentDictionary<long, Task> _work = new();
    private long _workId;
    private int _maxOrder;
    private TaskCompletionSource _completion = CompletedSource();

    private AbsolutePath _watchFolder;
    private AbsolutePath _destinationFolder;
    private Channel<AbsolutePath>? _channel;
    private WatchFolderSource? _source;
    private CancellationTokenSource? _cts;
    private Task? _consumer;

    public ManualDownloadAcquirer(ILogger<ManualDownloadAcquirer> logger, FileHashCache hashCache,
        IResource<FileHashCache> hashLimiter, DownloadDispatcher dispatcher, ManualDownloadAcquirerOptions options)
    {
        _logger = logger;
        _hashCache = hashCache;
        _hashLimiter = hashLimiter;
        _dispatcher = dispatcher;
        _options = options;
        _stableInterval = AtLeast(options.StableInterval, MinimumDelay);
        _lockRetryDelay = AtLeast(options.LockRetryDelay, MinimumDelay);
        _lockRetryCap = AtLeast(options.LockRetryCap, _lockRetryDelay);
    }

    private static TimeSpan AtLeast(TimeSpan value, TimeSpan floor)
    {
        return value < floor ? floor : value;
    }

    public IObservable<ManualDownloadEvent> Events => _events.AsObservable();
    public IObservable<ManualDownloadNotice> Notices => _notices.AsObservable();
    public AbsolutePath WatchFolder => _watchFolder;
    public AbsolutePath DestinationFolder => _destinationFolder;

    public IReadOnlyList<ManualDownloadItem> Snapshot()
    {
        lock (_gate)
        {
            return _entries.Values.OrderBy(e => e.Order).Select(e => e.ToItem()).ToList();
        }
    }

    public ManualDownloadItem? Current
    {
        get
        {
            lock (_gate)
            {
                return CurrentEntry()?.ToItem();
            }
        }
    }

    public ManualDownloadCounts Counts
    {
        get
        {
            lock (_gate)
            {
                return CountsLocked();
            }
        }
    }

    #region Lifecycle

    public async Task Start(IEnumerable<Archive> pending, AbsolutePath watchFolder, AbsolutePath destinationFolder,
        CancellationToken token)
    {
        await Stop();

        var archives = pending.ToList();
        foreach (var archive in archives)
        {
            if (string.IsNullOrWhiteSpace(archive.Name))
                throw new ArgumentException("Every archive needs a name; it is the file name it is placed under.",
                    nameof(pending));
            if (archive.Hash == default)
                throw new ArgumentException(
                    $"Archive {archive.Name} has no hash, so a download of it could never be verified.", nameof(pending));
            if (archive.Size <= 0)
                throw new ArgumentException($"Archive {archive.Name} has no size, so it cannot be matched.",
                    nameof(pending));
        }

        lock (_gate)
        {
            _entries.Clear();
            _seen.Clear();
            _noticed.Clear();
            _inFlight.Clear();
            _maxOrder = 0;
            foreach (var archive in archives)
            {
                if (_entries.ContainsKey(archive.Name))
                    throw new ArgumentException($"Archive {archive.Name} appears twice.", nameof(pending));
                _entries[archive.Name] = new Entry {Key = archive.Name, Archive = archive, Order = ++_maxOrder};
            }

            _bySize = _entries.Values.GroupBy(e => e.Archive.Size)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Order).ToArray());
            RefreshOpenSizesLocked();
            _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _watchFolder = watchFolder;
            _destinationFolder = destinationFolder;
        }

        destinationFolder.CreateDirectory();
        SweepIncoming(destinationFolder);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _cts = cts;

        await InventoryDestination(cts.Token);

        var channel = Channel.CreateUnbounded<AbsolutePath>(new UnboundedChannelOptions {SingleReader = true});
        _channel = channel;
        _consumer = Task.Run(() => Consume(channel.Reader, cts.Token), CancellationToken.None);
        _source = new WatchFolderSource(watchFolder, _options, channel.Writer, IsInteresting, Notify, _logger,
            cts.Token);

        lock (_gate)
        {
            CheckCompletionLocked();
        }
    }

    public async Task Stop()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts == null) return;

        cts.Cancel();

        var source = Interlocked.Exchange(ref _source, null);
        if (source != null)
            await source.DisposeAsync();

        _channel?.Writer.TryComplete();

        var consumer = _consumer;
        if (consumer != null)
        {
            try
            {
                await consumer;
            }
            catch (OperationCanceledException)
            {
            }
        }

        try
        {
            await Task.WhenAll(_work.Values.ToArray());
        }
        catch (OperationCanceledException)
        {
        }

        _consumer = null;
        _channel = null;
        cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Stop();
        lock (_publishGate)
        {
            if (_disposed) return;
            _disposed = true;
            _events.OnCompleted();
            _notices.OnCompleted();
            _events.Dispose();
            _notices.Dispose();
        }
    }

    /// <summary>
    ///     For containers and scopes disposed synchronously. Stopping waits for in-flight placements, none of
    ///     which need the calling thread.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public void SetWatchFolder(AbsolutePath folder)
    {
        WatchFolderSource? old;
        lock (_gate)
        {
            if (folder == _watchFolder && _source != null) return;
            _watchFolder = folder;
        }

        var cts = _cts;
        var channel = _channel;
        if (cts == null || channel == null)
            return;

        var replacement = new WatchFolderSource(folder, _options, channel.Writer, IsInteresting, Notify, _logger,
            cts.Token);
        old = Interlocked.Exchange(ref _source, replacement);

        Notify(new ManualDownloadNotice(ManualDownloadNoticeKind.WatchFolderChanged, folder,
            $"Now watching {folder}."));

        if (old != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await old.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Disposing previous watch folder source");
                }
            });
        }
    }

    public async Task Rescan(CancellationToken token)
    {
        var cts = _cts;
        if (cts == null) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);
        await InventoryDestination(linked.Token);
        var source = _source;
        if (source != null)
            await Task.Run(() => source.ScanNow(), linked.Token);
    }

    public Task WaitForCompletion(CancellationToken token)
    {
        return _completion.Task.WaitAsync(token);
    }

    #endregion

    #region Queue operations

    public void Skip(string key)
    {
        ManualDownloadEvent? evt = null;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry) || entry.State == ManualDownloadState.Moved) return;
            entry.Order = ++_maxOrder;
            evt = EventLocked(entry, entry.State);
        }

        Publish(evt);
    }

    public void Retry(string key)
    {
        ManualDownloadEvent? evt = null;
        AbsolutePath? requeue = null;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry)) return;
            if (entry.State is not (ManualDownloadState.WrongFile or ManualDownloadState.Failed)) return;

            var previous = entry.State;
            entry.State = ManualDownloadState.Pending;
            entry.Message = null;
            entry.Progress = Percent.Zero;
            if (previous == ManualDownloadState.Failed)
                requeue = entry.CandidatePath;
            else
                entry.CandidatePath = null;
            evt = EventLocked(entry, previous);
        }

        Publish(evt);

        if (requeue != null)
            _channel?.Writer.TryWrite(requeue.Value);
        _source?.Kick();
    }

    public Task AddFileManually(string key, AbsolutePath file, CancellationToken token)
    {
        Entry? requested;
        ManualDownloadEvent? busy = null;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out requested))
                return Task.FromException(
                    new ArgumentException($"No manual download is pending under the name {key}.", nameof(key)));
            if (_inFlight.ContainsKey(file))
                busy = SetMessageLocked(requested, $"{file.FileName} is already being checked.");
            else
                _inFlight[file] = false;
        }

        if (busy != null)
        {
            Publish(busy);
            return Task.CompletedTask;
        }

        // Registered before the token is read, so a Stop that has already begun either cancels this
        // evaluation or waits for it; it never returns while the file is still being hashed or copied.
        // The registration is released from a continuation, so that Stop only resumes once the task the
        // caller holds has completed.
        var done = TrackWork();
        var work = EvaluatePicked(file, requested, token);
        work.ContinueWith(_ => done(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return work;
    }

    private async Task EvaluatePicked(AbsolutePath file, Entry requested, CancellationToken token)
    {
        try
        {
            var cts = _cts;
            using var linked = cts == null
                ? CancellationTokenSource.CreateLinkedTokenSource(token)
                : CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);
            await Evaluate(file, requested, linked.Token);
        }
        finally
        {
            lock (_gate)
            {
                _inFlight.Remove(file);
            }
        }
    }

    /// <summary>
    ///     Registers a unit of work that <see cref="Stop" /> waits for, before the work starts, so a unit that
    ///     finishes at once cannot outrun its own registration. The returned action marks it finished.
    /// </summary>
    private Action TrackWork()
    {
        var id = Interlocked.Increment(ref _workId);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _work[id] = finished.Task;
        return () =>
        {
            _work.TryRemove(id, out _);
            finished.TrySetResult();
        };
    }

    /// <summary>Units of work registered and not yet finished; zero whenever nothing is being evaluated.</summary>
    internal int WorkInFlight => _work.Count;

    #endregion

    #region Detection

    private bool IsInteresting(FileInfo file)
    {
        if (CandidateFile.IsIgnoredAttributes(file.Attributes)) return false;
        if (file.Length == 0) return false;
        var path = file.FullName.ToAbsolutePath();
        if (CandidateFile.IsPartialDownload(path, _options.PartialExtensions)) return false;
        lock (_gate)
        {
            if (_openSizes.Contains(file.Length)) return true;
            // Every item of this size is in place. A file not looked at yet still gets its one pass, for the
            // duplicate notice; one already hashed is not re-evaluated on every poll.
            return _bySize.ContainsKey(file.Length) && !_seen.ContainsKey(path);
        }
    }

    private async Task Consume(ChannelReader<AbsolutePath> reader, CancellationToken token)
    {
        try
        {
            await foreach (var path in reader.ReadAllAsync(token))
            {
                lock (_gate)
                {
                    if (_inFlight.ContainsKey(path))
                    {
                        // Reported again while being looked at, as happens when a file is created empty
                        // and then written. One more pass afterwards, so the change is not lost when the
                        // watcher is the only source.
                        _inFlight[path] = true;
                        continue;
                    }

                    _inFlight[path] = false;
                }

                var done = TrackWork();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Evaluate(path, null, token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "While evaluating {Path}", path);
                    }
                    finally
                    {
                        bool again;
                        lock (_gate)
                        {
                            again = _inFlight.TryGetValue(path, out var changed) && changed;
                            _inFlight.Remove(path);
                        }

                        done();

                        if (again && !token.IsCancellationRequested)
                            _channel?.Writer.TryWrite(path);
                    }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ChannelClosedException)
        {
        }
    }

    /// <summary>
    ///     The whole pipeline for one file: cheap rules, size match, wait for the writer to let go, hash, match
    ///     by hash, place. <paramref name="requested" /> is set when the user picked the file for a particular
    ///     item; rejections are then explained on that item.
    /// </summary>
    private async Task Evaluate(AbsolutePath path, Entry? requested, CancellationToken token)
    {
        var info = new FileInfo(path.ToString());
        if (!info.Exists)
        {
            Vanished(path);
            Reject(requested, path, $"{path.FileName} is not there any more.");
            return;
        }

        var attributes = info.Attributes;
        if (CandidateFile.IsIgnoredAttributes(attributes))
        {
            Reject(requested, path, $"{path.FileName} is hidden or a system file.");
            return;
        }

        if (CandidateFile.IsPartialDownload(path, _options.PartialExtensions))
        {
            Reject(requested, path, $"{path.FileName} is still being downloaded.");
            return;
        }

        if (info.Length == 0)
        {
            Reject(requested, path, $"{path.FileName} is empty.");
            return;
        }

        if (CandidateFile.IsCloudPlaceholder(attributes))
        {
            NoticeOnce(path, ManualDownloadNoticeKind.CloudPlaceholder,
                $"{path.FileName} is stored in the cloud rather than on this PC. Mark it \"Always keep on this device\" and it will be picked up.");
            Reject(requested, path, $"{path.FileName} is a cloud placeholder; mark it \"Always keep on this device\".");
            return;
        }

        var size = info.Length;
        Entry[]? sized;
        lock (_gate)
        {
            if (IsPlacedPathLocked(path)) return;
            _bySize.TryGetValue(size, out sized);
        }

        if (sized == null || sized.Length == 0)
        {
            if (requested != null)
                MarkWrong(requested, path,
                    $"{path.FileName} is {size:N0} bytes; {requested.Archive.Name} should be {requested.Archive.Size:N0} bytes. Wrong file or version?");
            return;
        }

        if (CandidateFile.HasPartialSibling(path, p => p.FileExists(), _options.PartialExtensions))
            return;

        Hash hash = default;
        var known = false;
        lock (_gate)
        {
            if (_seen.TryGetValue(path, out var seen) && seen.Size == size && seen.LastWrite == info.LastWriteTimeUtc)
            {
                hash = seen.Hash;
                known = true;
            }
        }

        if (!known)
        {
            var detected = MarkDetected(path, sized, requested);
            try
            {
                hash = await ReadHash(path, size, detected, token);
            }
            catch (OperationCanceledException)
            {
                Revert(detected, path, true);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read {Path}", path);
                Revert(detected, path, false);
                Reject(requested, path, $"{path.FileName} could not be read: {ex.Message}");
                return;
            }

            if (hash == default)
            {
                Revert(detected, path, false);
                return;
            }

            lock (_gate)
            {
                _seen[path] = (size, File.GetLastWriteTimeUtc(path.ToString()), hash);
            }
        }

        var targets = Match(path, hash, sized, requested);
        if (targets.Count == 0)
        {
            // The file is another item's size, so Match spoke to that item; the one the user picked it
            // for still has to hear why nothing happened.
            if (requested != null && !sized.Contains(requested))
            {
                var owner = sized.FirstOrDefault(e => e.Archive.Hash == hash);
                if (owner == null)
                    MarkWrong(requested, path,
                        $"{path.FileName} is {size:N0} bytes with hash {hash.ToHex()}, which matches nothing in the queue; {requested.Archive.Name} should be {requested.Archive.Size:N0} bytes with hash {requested.Archive.Hash.ToHex()}. Wrong file or version?");
                else
                    Publish(SetMessage(requested,
                        $"{path.FileName} is another copy of {owner.Archive.Name}, which is already in place. {requested.Archive.Name} is still needed."));
            }

            return;
        }

        try
        {
            await Place(targets, path, hash, size, token);
        }
        finally
        {
            if (requested != null && !targets.Contains(requested))
            {
                var placed = string.Join(", ", targets.Select(t => t.Archive.Name));
                var evt = SetMessage(requested,
                    $"{path.FileName} is {placed}, which has been placed. {requested.Archive.Name} is still needed.");
                Publish(evt);
            }
        }
    }

    private List<Entry> MarkDetected(AbsolutePath path, Entry[] sized, Entry? requested)
    {
        var detected = new List<Entry>();
        var events = new List<ManualDownloadEvent>();
        lock (_gate)
        {
            foreach (var entry in sized)
            {
                if (entry.State == ManualDownloadState.Moved || entry.Busy) continue;
                var claimable = entry.CandidatePath == null || entry.CandidatePath == path || entry == requested ||
                                entry.State is ManualDownloadState.Pending or ManualDownloadState.WrongFile
                                    or ManualDownloadState.Failed;
                if (!claimable) continue;

                var previous = entry.State;
                entry.State = ManualDownloadState.Detected;
                entry.CandidatePath = path;
                entry.Progress = Percent.Zero;
                entry.Message = $"Found {path.FileName}, waiting for it to finish.";
                detected.Add(entry);
                events.Add(EventLocked(entry, previous));
            }
        }

        Publish(events);
        return detected;
    }

    private void Revert(List<Entry> detected, AbsolutePath path, bool keepCandidate)
    {
        var events = new List<ManualDownloadEvent>();
        lock (_gate)
        {
            foreach (var entry in detected)
            {
                if (entry.Busy || entry.CandidatePath != path) continue;
                if (entry.State is not (ManualDownloadState.Detected or ManualDownloadState.Waiting
                    or ManualDownloadState.Verifying)) continue;

                var previous = entry.State;
                entry.State = ManualDownloadState.Pending;
                entry.CandidatePath = keepCandidate ? path : null;
                entry.Progress = Percent.Zero;
                entry.Message = null;
                events.Add(EventLocked(entry, previous));
            }
        }

        Publish(events);
    }

    /// <summary>
    ///     Waits for the file to stop growing, then for its writer to close it, then hashes it. Returns
    ///     default when the file went away or changed size, in which case the next poll will look again.
    /// </summary>
    private async Task<Hash> ReadHash(AbsolutePath path, long expectedSize, List<Entry> detected,
        CancellationToken token)
    {
        var attempt = 0;
        var native = path.ToString();

        while (true)
        {
            token.ThrowIfCancellationRequested();

            if (!await WaitForStableSize(native, token))
                return default;

            FileStream stream;
            try
            {
                stream = new FileStream(native, FileMode.Open, FileAccess.Read, FileShare.None, 1 << 16,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
            }
            catch (FileNotFoundException)
            {
                return default;
            }
            catch (DirectoryNotFoundException)
            {
                return default;
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                SetState(detected, path, ManualDownloadState.Waiting,
                    $"Your browser is still writing {path.FileName}; waiting for it to finish.");
                var delay = LockRetryDelay(attempt++);
                await Task.Delay(delay, token);
                continue;
            }

            await using (stream)
            {
                if (stream.Length != expectedSize)
                    return default;

                SetState(detected, path, ManualDownloadState.Verifying, $"Verifying {path.FileName}.");

                using var job = await _hashLimiter.Begin($"Hashing {path.FileName}", expectedSize, token);
                var lastPercent = -1;
                job.OnUpdate += (_, update) =>
                {
                    var percent = (int) (update.Progress.Value * 100);
                    if (percent == lastPercent) return;
                    lastPercent = percent;
                    SetProgress(detected, path, update.Progress);
                };

                var hash = await stream.HashingCopy(Stream.Null, token, job);
                token.ThrowIfCancellationRequested();
                return hash;
            }
        }
    }

    private async Task<bool> WaitForStableSize(string native, CancellationToken token)
    {
        var stable = 0;
        long lastSize = -1;
        var lastWrite = DateTime.MinValue;
        var required = Math.Max(1, _options.StableSamples);

        while (stable < required)
        {
            var info = new FileInfo(native);
            if (!info.Exists) return false;

            if (info.Length == lastSize && info.LastWriteTimeUtc == lastWrite)
            {
                stable++;
            }
            else
            {
                stable = 1;
                lastSize = info.Length;
                lastWrite = info.LastWriteTimeUtc;
            }

            if (stable < required)
                await Task.Delay(_stableInterval, token);
        }

        return true;
    }

    private TimeSpan LockRetryDelay(int attempt)
    {
        var delay = _lockRetryDelay;
        for (var i = 0; i < attempt && delay < _lockRetryCap; i++)
            delay += delay;
        return delay > _lockRetryCap ? _lockRetryCap : delay;
    }

    private static bool IsSharingViolation(IOException ex)
    {
        return IOErrors.IsSharingViolation(ex);
    }

    private static bool IsDiskFull(IOException ex)
    {
        return IOErrors.IsDiskFull(ex);
    }

    #endregion

    #region Matching

    /// <summary>
    ///     Claims every pending item whose hash matches, in queue order. A hash that matches nothing marks the
    ///     size siblings that were waiting on this file as WrongFile; a hash that matches only items already
    ///     in place is a duplicate. Anything not claimed here is left exactly as it was.
    /// </summary>
    private List<Entry> Match(AbsolutePath path, Hash hash, Entry[] sized, Entry? requested)
    {
        var targets = new List<Entry>();
        var events = new List<ManualDownloadEvent>();
        var duplicate = false;
        string? duplicateOf = null;

        lock (_gate)
        {
            var matched = sized.Where(e => e.Archive.Hash == hash).ToList();

            if (matched.Count == 0)
            {
                foreach (var entry in sized)
                {
                    if (entry.State == ManualDownloadState.Moved || entry.Busy) continue;
                    if (entry.CandidatePath != path && entry != requested) continue;
                    // A rejected file is remembered, so re-polls of it come through here without a rehash;
                    // an item already showing that verdict does not need to be told again.
                    if (entry.State == ManualDownloadState.WrongFile && entry.CandidatePath == path) continue;

                    var previous = entry.State;
                    entry.State = ManualDownloadState.WrongFile;
                    entry.CandidatePath = path;
                    entry.Progress = Percent.Zero;
                    // The hashes go to the log. On screen they tell the user nothing they can act on, and
                    // the sentence has to fit a card.
                    _logger.LogInformation(
                        "{File} is the size {Archive} expects but hashes to {Actual} rather than {Expected}",
                        path.FileName, entry.Archive.Name, hash.ToHex(), entry.Archive.Hash.ToHex());
                    entry.Message =
                        $"{path.FileName} is the right size but not the right file, probably a different version";
                    events.Add(EventLocked(entry, previous));
                }
            }
            else
            {
                foreach (var entry in matched)
                {
                    if (entry.State == ManualDownloadState.Moved || entry.Busy) continue;
                    entry.Busy = true;
                    var previous = entry.State;
                    entry.State = ManualDownloadState.Verifying;
                    entry.CandidatePath = path;
                    entry.Progress = Percent.Zero;
                    entry.Message = $"{path.FileName} verified; moving it into place.";
                    targets.Add(entry);
                    events.Add(EventLocked(entry, previous));
                }

                if (targets.Count == 0 && matched.All(e => e.State == ManualDownloadState.Moved))
                {
                    duplicate = _noticed.Add(path);
                    duplicateOf = matched[0].Archive.Name;
                }

                // Siblings that showed this file as theirs go back to waiting for their own.
                foreach (var entry in sized)
                {
                    if (targets.Contains(entry) || entry.Busy) continue;
                    if (entry.CandidatePath != path) continue;
                    if (entry.State is not (ManualDownloadState.Detected or ManualDownloadState.Waiting
                        or ManualDownloadState.Verifying)) continue;

                    var previous = entry.State;
                    entry.State = ManualDownloadState.Pending;
                    entry.CandidatePath = null;
                    entry.Progress = Percent.Zero;
                    entry.Message = null;
                    events.Add(EventLocked(entry, previous));
                }
            }
        }

        Publish(events);

        if (duplicate)
        {
            Notify(new ManualDownloadNotice(ManualDownloadNoticeKind.DuplicateIgnored, path,
                $"{path.FileName} is another copy of {duplicateOf}, which is already in place. It was left where it is."));
        }

        return targets;
    }

    #endregion

    #region Placement

    /// <summary>
    ///     Places one verified file for every item that claimed it: copies for all but the last, and a move for
    ///     the last, so that two archives with identical bytes both end up in place.
    /// </summary>
    private async Task Place(List<Entry> targets, AbsolutePath path, Hash hash, long size, CancellationToken token)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            var entry = targets[i];
            var last = i == targets.Count - 1;
            try
            {
                await PlaceOne(entry, path, hash, size, last, token);
            }
            catch (OperationCanceledException)
            {
                var events = new List<ManualDownloadEvent>();
                for (var j = i + 1; j < targets.Count; j++)
                    events.Add(Finish(targets[j], ManualDownloadState.Pending, null, null));
                Publish(events);
                throw;
            }
        }
    }

    private async Task PlaceOne(Entry entry, AbsolutePath path, Hash hash, long size, bool mayMove,
        CancellationToken token)
    {
        var dest = _destinationFolder.Combine(entry.Archive.Name);
        var name = entry.Archive.Name;
        string message;

        try
        {
            if (path == dest)
            {
                message = $"{name} is already in place.";
            }
            else if (await IsAlreadyCorrect(dest, size, hash, token))
            {
                message = $"{name} was already present; {path.FileName} was left where it is.";
            }
            else if (mayMove && !_options.ForceCopyPlacement && SameVolume(path, dest))
            {
                await path.MoveToAsync(dest, true, token);
                message = $"Moved {path.FileName} into place.";
            }
            else
            {
                await CopyIntoPlace(entry, path, dest, hash, size, mayMove, token);
                message = mayMove ? $"Copied {path.FileName} into place." : $"Copied {path.FileName} into place as {name}.";
            }

            await WriteMeta(entry.Archive, dest, token);
            await _hashCache.FileHashWriteCache(dest, hash);

            _logger.LogInformation("Manual download {Name} placed from {Path}", name, path);
            Publish(Finish(entry, ManualDownloadState.Moved, dest, message));
        }
        catch (OperationCanceledException)
        {
            Publish(Finish(entry, ManualDownloadState.Pending, null, null));
            throw;
        }
        catch (IOException ex) when (IsDiskFull(ex))
        {
            _logger.LogError(ex, "Out of disk space placing {Name}", name);
            Publish(Finish(entry, ManualDownloadState.Failed, null, DiskFullMessage(dest, size)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not place {Name} from {Path}", name, path);
            Publish(Finish(entry, ManualDownloadState.Failed, null, $"Could not move {path.FileName} into place: {ex.Message}"));
        }
    }

    private async Task<bool> IsAlreadyCorrect(AbsolutePath dest, long size, Hash hash, CancellationToken token)
    {
        if (!dest.FileExists()) return false;
        try
        {
            if (dest.Size() != size) return false;
            return await _hashCache.FileHashCachedAsync(dest, token) == hash;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check the existing {Dest}", dest);
            return false;
        }
    }

    private async Task CopyIntoPlace(Entry entry, AbsolutePath src, AbsolutePath dest, Hash expected, long size,
        bool deleteSource, CancellationToken token)
    {
        var incoming = _destinationFolder.Combine(RandomName.Next()).WithExtension(Ext.WjIncoming);
        try
        {
            Hash copied;
            {
                await using var input = src.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var output = incoming.Open(FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var job = await _hashLimiter.Begin($"Copying {src.FileName}", size, token);
                var lastPercent = -1;
                job.OnUpdate += (_, update) =>
                {
                    var percent = (int) (update.Progress.Value * 100);
                    if (percent == lastPercent) return;
                    lastPercent = percent;
                    Publish(SetProgress(entry, update.Progress));
                };
                copied = await input.HashingCopy(output, token, job);
                token.ThrowIfCancellationRequested();
            }

            if (copied != expected)
                throw new IOException(
                    $"the copy of {src.FileName} did not verify (expected {expected.ToHex()}, got {copied.ToHex()}); the disk may be failing");

            await incoming.MoveToAsync(dest, true, token);

            if (deleteSource)
                TryDelete(src);
        }
        finally
        {
            if (incoming.FileExists())
                TryDelete(incoming);
        }
    }

    private void TryDelete(AbsolutePath file)
    {
        try
        {
            File.Delete(file.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete {File}", file);
        }
    }

    private static bool SameVolume(AbsolutePath a, AbsolutePath b)
    {
        var rootA = Path.GetPathRoot(a.ToString());
        var rootB = Path.GetPathRoot(b.ToString());
        return string.Equals(rootA, rootB, StringComparison.OrdinalIgnoreCase);
    }

    private static string DiskFullMessage(AbsolutePath dest, long size)
    {
        try
        {
            var root = Path.GetPathRoot(dest.ToString()) ?? dest.ToString();
            var free = new DriveInfo(root).AvailableFreeSpace;
            var shortfall = Math.Max(0, size - free);
            return
                $"Not enough space on {root} for {dest.FileName}: {size.ToFileSizeString()} needed, {free.ToFileSizeString()} free, {shortfall.ToFileSizeString()} short. Free some space and retry.";
        }
        catch (Exception)
        {
            return $"Not enough space for {dest.FileName} ({size.ToFileSizeString()}). Free some space and retry.";
        }
    }

    private async Task WriteMeta(Archive archive, AbsolutePath dest, CancellationToken token)
    {
        var lines = new List<string> {"[General]", "installed=true"};
        try
        {
            lines.AddRange(_dispatcher.MetaIni(archive));
        }
        catch (NotImplementedException)
        {
        }

        await dest.WithExtension(Ext.Meta).WriteAllLinesAsync(lines, token);
    }

    private static void SweepIncoming(AbsolutePath destination)
    {
        if (!destination.DirectoryExists()) return;
        foreach (var stale in destination.EnumerateFiles(Ext.WjIncoming, false))
        {
            try
            {
                File.Delete(stale.ToString());
            }
            catch (Exception)
            {
                // Left for the next run.
            }
        }
    }

    /// <summary>Marks anything already correct in the destination as in place without touching a file.</summary>
    private async Task InventoryDestination(CancellationToken token)
    {
        List<Entry> candidates;
        lock (_gate)
        {
            candidates = _entries.Values
                .Where(e => e.State != ManualDownloadState.Moved && !e.Busy)
                .OrderBy(e => e.Order)
                .ToList();
        }

        foreach (var entry in candidates)
        {
            token.ThrowIfCancellationRequested();
            var dest = _destinationFolder.Combine(entry.Archive.Name);
            if (!await IsAlreadyCorrect(dest, entry.Archive.Size, entry.Archive.Hash, token)) continue;

            lock (_gate)
            {
                if (entry.State == ManualDownloadState.Moved || entry.Busy) continue;
                entry.Busy = true;
            }

            try
            {
                if (!dest.WithExtension(Ext.Meta).FileExists())
                    await WriteMeta(entry.Archive, dest, token);
                Publish(Finish(entry, ManualDownloadState.Moved, dest, $"{entry.Archive.Name} is already present."));
            }
            catch (OperationCanceledException)
            {
                Publish(Finish(entry, ManualDownloadState.Pending, null, null));
                throw;
            }
        }
    }

    #endregion

    #region State helpers (all under _gate; events published after leaving it)

    private Entry? CurrentEntry()
    {
        return _entries.Values.Where(e => e.State != ManualDownloadState.Moved).MinBy(e => e.Order);
    }

    private ManualDownloadCounts CountsLocked()
    {
        int moved = 0, pending = 0, wrong = 0, failed = 0, inProgress = 0;
        foreach (var entry in _entries.Values)
        {
            switch (entry.State)
            {
                case ManualDownloadState.Moved:
                    moved++;
                    break;
                case ManualDownloadState.Pending:
                    pending++;
                    break;
                case ManualDownloadState.WrongFile:
                    wrong++;
                    break;
                case ManualDownloadState.Failed:
                    failed++;
                    break;
                default:
                    inProgress++;
                    break;
            }
        }

        return new ManualDownloadCounts(_entries.Count, moved, pending, wrong, failed, inProgress);
    }

    private ManualDownloadEvent EventLocked(Entry entry, ManualDownloadState previous)
    {
        return new ManualDownloadEvent(entry.ToItem(), previous, CountsLocked());
    }

    /// <summary>
    ///     A file this acquirer put in the destination, or is putting there right now. When the watch folder
    ///     is the destination the watcher reports our own renames, and between the rename and the item
    ///     being marked Moved the file must not be taken for a duplicate.
    /// </summary>
    private bool IsPlacedPathLocked(AbsolutePath path)
    {
        if (path.Depth > 1 && path.Parent == _destinationFolder &&
            _entries.TryGetValue(path.FileName.ToString(), out var owner) &&
            (owner.Busy || owner.State == ManualDownloadState.Moved))
            return true;

        foreach (var entry in _entries.Values)
        {
            if (entry.PlacedPath == path) return true;
        }

        return false;
    }

    private void CheckCompletionLocked()
    {
        if (_entries.Values.All(e => e.State == ManualDownloadState.Moved))
            _completion.TrySetResult();
    }

    private ManualDownloadEvent Finish(Entry entry, ManualDownloadState state, AbsolutePath? placed, string? message)
    {
        lock (_gate)
        {
            var previous = entry.State;
            entry.Busy = false;
            entry.State = state;
            entry.Message = message;
            if (state == ManualDownloadState.Moved)
            {
                entry.PlacedPath = placed;
                entry.Progress = Percent.One;
                RefreshOpenSizesLocked();
            }
            else
            {
                entry.Progress = Percent.Zero;
            }

            CheckCompletionLocked();
            return EventLocked(entry, previous);
        }
    }

    /// <summary>Must be called under <see cref="_gate" /> whenever an item is marked Moved.</summary>
    private void RefreshOpenSizesLocked()
    {
        _openSizes = _entries.Values
            .Where(e => e.State != ManualDownloadState.Moved)
            .Select(e => e.Archive.Size)
            .ToHashSet();
    }

    private void SetState(List<Entry> detected, AbsolutePath path, ManualDownloadState state, string message)
    {
        var events = new List<ManualDownloadEvent>();
        lock (_gate)
        {
            foreach (var entry in detected)
            {
                if (entry.Busy || entry.CandidatePath != path || entry.State == ManualDownloadState.Moved) continue;
                // Every lock retry lands here; the item is only told once.
                if (entry.State == state && entry.Message == message) continue;
                var previous = entry.State;
                entry.State = state;
                entry.Message = message;
                events.Add(EventLocked(entry, previous));
            }
        }

        Publish(events);
    }

    private void SetProgress(List<Entry> detected, AbsolutePath path, Percent progress)
    {
        var events = new List<ManualDownloadEvent>();
        lock (_gate)
        {
            foreach (var entry in detected)
            {
                if (entry.Busy || entry.CandidatePath != path || entry.State != ManualDownloadState.Verifying) continue;
                entry.Progress = progress;
                events.Add(EventLocked(entry, entry.State));
            }
        }

        Publish(events);
    }

    private ManualDownloadEvent SetProgress(Entry entry, Percent progress)
    {
        lock (_gate)
        {
            entry.Progress = progress;
            return EventLocked(entry, entry.State);
        }
    }

    private ManualDownloadEvent SetMessage(Entry entry, string message)
    {
        lock (_gate)
        {
            return SetMessageLocked(entry, message);
        }
    }

    private ManualDownloadEvent SetMessageLocked(Entry entry, string message)
    {
        entry.Message = message;
        return EventLocked(entry, entry.State);
    }

    private void MarkWrong(Entry entry, AbsolutePath path, string message)
    {
        ManualDownloadEvent? evt = null;
        lock (_gate)
        {
            if (entry.State == ManualDownloadState.Moved || entry.Busy) return;
            var previous = entry.State;
            entry.State = ManualDownloadState.WrongFile;
            entry.CandidatePath = path;
            entry.Progress = Percent.Zero;
            entry.Message = message;
            evt = EventLocked(entry, previous);
        }

        Publish(evt);
    }

    private void Reject(Entry? requested, AbsolutePath path, string message)
    {
        if (requested == null) return;
        MarkWrong(requested, path, message);
    }

    /// <summary>A file a watched item was waiting on has gone; the item goes back to waiting for anything.</summary>
    private void Vanished(AbsolutePath path)
    {
        var events = new List<ManualDownloadEvent>();
        lock (_gate)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Busy || entry.CandidatePath != path) continue;
                if (entry.State is not (ManualDownloadState.Detected or ManualDownloadState.Waiting)) continue;
                var previous = entry.State;
                entry.State = ManualDownloadState.Pending;
                entry.CandidatePath = null;
                entry.Progress = Percent.Zero;
                entry.Message = null;
                events.Add(EventLocked(entry, previous));
            }
        }

        Publish(events);
    }

    private void NoticeOnce(AbsolutePath path, ManualDownloadNoticeKind kind, string message)
    {
        lock (_gate)
        {
            if (!_noticed.Add(path)) return;
        }

        Notify(new ManualDownloadNotice(kind, path, message));
    }

    // Nothing is published once disposed: state changes still happen, but the subjects are gone and a
    // late event must not turn a cancellation into an ObjectDisposedException.

    private void Notify(ManualDownloadNotice notice)
    {
        lock (_publishGate)
        {
            if (_disposed) return;
            _notices.OnNext(notice);
        }
    }

    private void Publish(ManualDownloadEvent? evt)
    {
        if (evt == null) return;
        lock (_publishGate)
        {
            if (_disposed) return;
            _events.OnNext(evt);
        }
    }

    private void Publish(List<ManualDownloadEvent> events)
    {
        if (events.Count == 0) return;
        lock (_publishGate)
        {
            if (_disposed) return;
            foreach (var evt in events)
                _events.OnNext(evt);
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    #endregion
}
