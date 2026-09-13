#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

/// <summary>
///     An acquirer whose outcome the test scripts: <see cref="Start" /> records what it was given and
///     <see cref="Results" /> says what each item ended as. Completion is immediate unless
///     <see cref="WhileWaiting" /> is set.
/// </summary>
public sealed class FakeManualDownloadAcquirer : IManualDownloadAcquirer
{
    private readonly Subject<ManualDownloadEvent> _events = new();
    private readonly Subject<ManualDownloadNotice> _notices = new();
    private List<Archive> _started = new();

    /// <summary>Per archive name: the state and placed path to report once started.</summary>
    public Dictionary<string, (ManualDownloadState State, AbsolutePath? Placed)> Results { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Archive> Started => _started;
    public int StopCalls { get; private set; }

    /// <summary>
    ///     Stands in for the user taking their time: it runs inside <see cref="WaitForCompletion" />, so a
    ///     test can look at what the rest of the run is doing while a check is waiting on the queue.
    /// </summary>
    public Func<CancellationToken, Task>? WhileWaiting { get; set; }

    public IObservable<ManualDownloadEvent> Events => _events.AsObservable();
    public IObservable<ManualDownloadNotice> Notices => _notices.AsObservable();
    public AbsolutePath WatchFolder { get; private set; }
    public AbsolutePath DestinationFolder { get; private set; }

    public IReadOnlyList<ManualDownloadItem> Snapshot()
    {
        return _started.Select((a, i) =>
        {
            var (state, placed) = Results.TryGetValue(a.Name, out var r) ? r : (ManualDownloadState.Pending, null);
            return new ManualDownloadItem(a.Name, a, i + 1, state, null, placed, Percent.Zero, null);
        }).ToList();
    }

    public ManualDownloadItem? Current => Snapshot().FirstOrDefault(i => i.State != ManualDownloadState.Moved);

    public ManualDownloadCounts Counts
    {
        get
        {
            var items = Snapshot();
            var moved = items.Count(i => i.State == ManualDownloadState.Moved);
            return new ManualDownloadCounts(items.Count, moved, items.Count - moved, 0, 0, 0);
        }
    }

    public Task Start(IEnumerable<Archive> pending, AbsolutePath watchFolder, AbsolutePath destinationFolder,
        CancellationToken token)
    {
        _started = pending.ToList();
        WatchFolder = watchFolder;
        DestinationFolder = destinationFolder;
        return Task.CompletedTask;
    }

    public void SetWatchFolder(AbsolutePath folder)
    {
        WatchFolder = folder;
    }

    public void Skip(string key)
    {
    }

    public void Retry(string key)
    {
    }

    public Task AddFileManually(string key, AbsolutePath file, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public Task Rescan(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public Task WaitForCompletion(CancellationToken token)
    {
        return WhileWaiting?.Invoke(token) ?? Task.CompletedTask;
    }

    public Task Stop()
    {
        StopCalls++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _events.Dispose();
        _notices.Dispose();
        return ValueTask.CompletedTask;
    }
}
