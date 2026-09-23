using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.Installer.Preflight;

namespace Wabbajack;

/// <summary>
///     Every archive the runner has reported on, as a sorted, virtualizable list collapsed into three bands:
///     what is done, what is being fetched now, and what is still to come. Only the current band is listed
///     item by item; the other two stand as a count and a total until the user opens them, which is what keeps
///     a thousand finished rows out of the way. Archive events arrive from download threads by the thousand;
///     they are batched on the UI thread, where a progress tick goes straight to its row and only a state
///     change reaches the cache, so each row keeps its instance for its whole life and the list re-sorts
///     no more often than an archive actually moves.
/// </summary>
public partial class BulkDownloadsVM : ViewModel
{
    private static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan FooterTick = TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     A band reports a reset once a batch passes this many changes, and a reset is the one thing the
    ///     flat list cannot mirror: it has to reload, which drops the ListBox's realized rows and the
    ///     user's scroll position with them. The default of 25 is crossed by any run where a few dozen
    ///     archives finish inside one batch window, which is most of them. This is set high enough that
    ///     what a run produces stays incremental, and low enough that a whole inventory arriving at once
    ///     still takes the cheap path: at 5,000 rows one reload costs about 80ms against roughly 1.2s of
    ///     individual inserts.
    /// </summary>
    private static readonly SortAndBindOptions GranularSort = new() { ResetThreshold = 1000 };

    private static readonly IComparer<ArchiveRowVM> BucketThenName = Comparer<ArchiveRowVM>.Create((a, b) =>
    {
        var bucket = a.SortBucket.CompareTo(b.SortBucket);
        return bucket != 0 ? bucket : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    });

    private readonly SourceCache<ArchiveStatus, string> _archives = new(s => s.Archive.Name);

    /// <summary>
    ///     The bands, in the order they are shown; the index into this is the index into everything else.
    ///     The header knows only what it draws, so which rows fall in it is kept here beside it.
    /// </summary>
    private readonly (ArchiveGroup Kind, ArchiveGroupVM Band)[] _groups =
    {
        (ArchiveGroup.Done, new ArchiveGroupVM("Done", false)),
        (ArchiveGroup.Current, new ArchiveGroupVM("Current", true)),
        (ArchiveGroup.Remaining, new ArchiveGroupVM("Remaining", false))
    };

    private readonly ReadOnlyObservableCollection<ArchiveRowVM>[] _groupRows;

    /// <summary>
    ///     What the list binds to: the three headers with the rows of each opened band spliced in after it.
    ///     Kept in step with the band collections one change at a time, so opening a band is the only thing
    ///     that resets the list, and virtualization sees a plain flat list either way.
    /// </summary>
    private readonly ObservableCollectionExtended<object> _items = new();

    /// <summary>
    ///     Every row by archive name, so an update that moves nothing can be applied to the row without
    ///     going through the cache.
    /// </summary>
    private readonly Dictionary<string, ArchiveRowVM> _rows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     How many rows of each band are spliced into <see cref="_items" />, or null where the band is
    ///     folded away. Offsets come from this rather than from <see cref="ArchiveGroupVM.IsExpanded" />:
    ///     the flag is what the band should look like, this is what the flat list actually holds, and only
    ///     the second one can be used to index it.
    /// </summary>
    private readonly int?[] _spliced;

    public BulkDownloadsVM(PreflightRunner runner, IObservable<ArchiveStatus> changes, IObservable<string> downloadSpeed)
    {
        _spliced = new int?[_groups.Length];

        FooterText = string.Empty;
        Items = new ReadOnlyObservableCollection<object>(_items);

        var rows = _archives.Connect()
            .TransformWithInlineUpdate(status =>
            {
                var row = new ArchiveRowVM(status.Archive, status.Target);
                row.Apply(status);
                _rows[row.Key] = row;
                return row;
            }, (row, status) => row.Apply(status))
            .Publish();

        _groupRows = new ReadOnlyObservableCollection<ArchiveRowVM>[_groups.Length];
        for (var i = 0; i < _groups.Length; i++)
        {
            var group = _groups[i].Kind;
            rows.Filter(row => row.Group == group)
                .SortAndBind(out var bound, BucketThenName, GranularSort)
                .Subscribe()
                .DisposeWith(CompositeDisposable);
            _groupRows[i] = bound;
        }

        rows.Connect().DisposeWith(CompositeDisposable);

        // Whatever the runner already knows goes in as a single changeset.
        _archives.Edit(cache => cache.AddOrUpdate(runner.Archives.Values));

        Rebuild();

        for (var i = 0; i < _groups.Length; i++)
        {
            var index = i;
            NotifyCollectionChangedEventHandler handler = (_, e) => OnGroupChanged(index, e);
            ((INotifyCollectionChanged) _groupRows[index]).CollectionChanged += handler;
            Disposable.Create(() => ((INotifyCollectionChanged) _groupRows[index]).CollectionChanged -= handler)
                .DisposeWith(CompositeDisposable);

            // Folding a band and rebuilding the flat list are one step. IsExpanded is only ever set by
            // the band header's own command, so this is already on the UI thread; deferring the rebuild
            // would leave a window where the flag says one layout and the list holds another, and a batch
            // landing in that window splices rows at an offset the list does not have.
            _groups[index].Band.WhenAnyValue(x => x.IsExpanded)
                .Skip(1)
                .Subscribe(_ => Rebuild())
                .DisposeWith(CompositeDisposable);
        }

        changes
            .Buffer(BatchWindow)
            .Where(batch => batch.Count > 0)
            .ObserveOnGuiThread()
            .Subscribe(Apply)
            .DisposeWith(CompositeDisposable);

        // Cancelling stops whatever the runner is on, and IsDownloading is only sampled, so it can still be
        // set for half a second after the downloads are done. What to stop is decided from the runner here,
        // or the button would cancel the check that happens to have started since.
        StopDownloadsCommand = ReactiveCommand.Create(() =>
        {
            if (runner.Checks.Any(c => c.Id == PreflightCheckIds.AutomatedDownloads
                                       && c.State == PreflightState.Running))
                runner.Cancel();
        }, this.WhenAnyValue(x => x.IsDownloading));

        var speed = downloadSpeed.StartWith(string.Empty).Replay(1);
        speed.Connect().DisposeWith(CompositeDisposable);

        // The footer walks every row, so it is sampled rather than driven by events, and only while shown.
        // The band summaries ride the same tick for the same reason.
        this.WhenActivated(disposables =>
        {
            Observable.Interval(FooterTick)
                .StartWith(0L)
                .ObserveOnGuiThread()
                .WithLatestFrom(speed, (_, s) => s)
                .Subscribe(s => FooterText = Summarize(s))
                .DisposeWith(disposables);
        });

        FooterText = Summarize(string.Empty);
    }

    /// <summary>Band headers and the rows of whichever bands are open, in one list for one virtualized view.</summary>
    public ReadOnlyObservableCollection<object> Items { get; }

    [Reactive] public partial string FooterText { get; set; }

    /// <summary>True while at least one archive is being fetched; sampled with the footer.</summary>
    [Reactive] public partial bool IsDownloading { get; set; }

    public ReactiveCommand<Unit, Unit> StopDownloadsCommand { get; }

    /// <summary>
    ///     One batch of archive updates. Most of them are progress ticks, and a tick moves nothing: the row
    ///     notifies its own bindings, and neither the band a row sits in nor its place in the sort follows
    ///     from <see cref="ArchiveRowVM.Progress" />. Pushing those through the cache turned every tick into
    ///     a changeset refresh, and a batch of them past <c>SortAndBindOptions.ResetThreshold</c> reset the
    ///     bound list, which threw the user's scroll position away several times a second. Only an archive
    ///     that has actually changed state reaches the cache.
    /// </summary>
    private void Apply(IList<ArchiveStatus> batch)
    {
        List<ArchiveStatus>? moved = null;
        foreach (var status in batch)
        {
            if (_rows.TryGetValue(status.Archive.Name, out var row) && row.State == status.State)
            {
                row.Apply(status);
                continue;
            }

            (moved ??= new List<ArchiveStatus>()).Add(status);
        }

        if (moved == null) return;
        _archives.Edit(cache =>
        {
            foreach (var status in moved) cache.AddOrUpdate(status);
        });
    }

    /// <summary>The index in <see cref="_items" /> the rows of band <paramref name="group" /> start at.</summary>
    private int RowStart(int group)
    {
        var start = group + 1;
        for (var i = 0; i < group; i++)
            start += _spliced[i] ?? 0;
        return start;
    }

    private void Rebuild()
    {
        var flat = new List<object>();
        for (var i = 0; i < _groups.Length; i++)
        {
            flat.Add(_groups[i].Band);
            if (!_groups[i].Band.IsExpanded)
            {
                _spliced[i] = null;
                continue;
            }

            flat.AddRange(_groupRows[i]);
            _spliced[i] = _groupRows[i].Count;
        }

        _items.Load(flat);
    }

    /// <summary>
    ///     Mirrors one band's change into the flat list. Bands ahead of this one are untouched by it, so their
    ///     counts are already settled and the offset is exact; a closed band contributes nothing but its header.
    /// </summary>
    private void OnGroupChanged(int group, NotifyCollectionChangedEventArgs e)
    {
        // What the flat list holds for this band, not what the band would like to show: nothing of a
        // folded band is in the list to move.
        if (_spliced[group] is not { } spliced) return;

        var start = RowStart(group);
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                for (var i = 0; i < e.NewItems.Count; i++)
                    _items.Insert(start + e.NewStartingIndex + i, e.NewItems[i]!);
                _spliced[group] = spliced + e.NewItems.Count;
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                for (var i = 0; i < e.OldItems.Count; i++)
                    _items.RemoveAt(start + e.OldStartingIndex);
                _spliced[group] = spliced - e.OldItems.Count;
                break;
            case NotifyCollectionChangedAction.Move:
                _items.Move(start + e.OldStartingIndex, start + e.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Replace when e.NewItems != null:
                for (var i = 0; i < e.NewItems.Count; i++)
                    _items[start + e.NewStartingIndex + i] = e.NewItems[i]!;
                break;
            default:
                Rebuild();
                break;
        }
    }

    /// <summary>Walks every row once, updating the three band summaries and returning the footer line.</summary>
    private string Summarize(string speed)
    {
        var total = 0;
        var done = 0;
        long totalBytes = 0;
        long doneBytes = 0;
        var downloading = false;

        for (var i = 0; i < _groups.Length; i++)
        {
            var count = 0;
            long bytes = 0;
            foreach (var row in _groupRows[i])
            {
                count++;
                bytes += row.Size;
                if (row.IsDone) doneBytes += row.Size;
                else if (row.State == ArchiveState.Downloading)
                {
                    downloading = true;
                    doneBytes += (long) (row.Progress * row.Size);
                }
            }

            _groups[i].Band.Summarize(count, bytes);
            total += count;
            totalBytes += bytes;
            if (_groups[i].Kind == ArchiveGroup.Done) done = count;
        }

        IsDownloading = downloading;
        if (total == 0) return string.Empty;

        var text = $"{done:N0} of {total:N0} · {doneBytes.ToFileSizeString()} of {totalBytes.ToFileSizeString()}";
        if (downloading && !string.IsNullOrWhiteSpace(speed))
            text += $" · {speed}";
        return text;
    }
}
