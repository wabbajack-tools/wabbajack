using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.Installer.Preflight;

namespace Wabbajack;

/// <summary>
///     Every archive the runner has reported on, as a sorted, virtualizable list. Archive events arrive from
///     download threads by the thousand; they are batched and applied as one cache edit per batch on the UI
///     thread, and each row keeps its instance for its whole life so the list only re-sorts on a state change.
/// </summary>
public partial class BulkDownloadsVM : ViewModel
{
    private static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan FooterTick = TimeSpan.FromMilliseconds(500);

    private static readonly IComparer<ArchiveRowVM> BucketThenName = Comparer<ArchiveRowVM>.Create((a, b) =>
    {
        var bucket = a.SortBucket.CompareTo(b.SortBucket);
        return bucket != 0 ? bucket : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    });

    private readonly SourceCache<ArchiveStatus, string> _archives = new(s => s.Archive.Name);
    private readonly ReadOnlyObservableCollection<ArchiveRowVM> _rows;

    public BulkDownloadsVM(PreflightRunner runner, IObservable<ArchiveStatus> changes, IObservable<string> downloadSpeed)
    {
        FooterText = string.Empty;

        _archives.Connect()
            .TransformWithInlineUpdate(status =>
            {
                var row = new ArchiveRowVM(status.Archive, status.Target);
                row.Apply(status);
                return row;
            }, (row, status) => row.Apply(status))
            .SortAndBind(out _rows, BucketThenName)
            .Subscribe()
            .DisposeWith(CompositeDisposable);

        // Whatever the runner already knows goes in as a single changeset.
        _archives.Edit(cache => cache.AddOrUpdate(runner.Archives.Values));

        changes
            .Buffer(BatchWindow)
            .Where(batch => batch.Count > 0)
            .ObserveOnGuiThread()
            .Subscribe(batch => _archives.Edit(cache =>
            {
                foreach (var status in batch) cache.AddOrUpdate(status);
            }))
            .DisposeWith(CompositeDisposable);

        StopDownloadsCommand = ReactiveCommand.Create(runner.Cancel, this.WhenAnyValue(x => x.IsDownloading));

        var speed = downloadSpeed.StartWith(string.Empty).Replay(1);
        speed.Connect().DisposeWith(CompositeDisposable);

        // The footer walks every row, so it is sampled rather than driven by events, and only while shown.
        this.WhenActivated(disposables =>
        {
            Observable.Interval(FooterTick)
                .StartWith(0L)
                .ObserveOnGuiThread()
                .WithLatestFrom(speed, (_, s) => s)
                .Subscribe(s => FooterText = Footer(s))
                .DisposeWith(disposables);
        });
    }

    public ReadOnlyObservableCollection<ArchiveRowVM> Rows => _rows;

    [Reactive] public partial string FooterText { get; set; }

    /// <summary>True while at least one archive is being fetched; sampled with the footer.</summary>
    [Reactive] public partial bool IsDownloading { get; set; }

    public ReactiveCommand<Unit, Unit> StopDownloadsCommand { get; }

    private string Footer(string speed)
    {
        var total = 0;
        var done = 0;
        long totalBytes = 0;
        long doneBytes = 0;
        var downloading = false;
        foreach (var row in _rows)
        {
            total++;
            totalBytes += row.Size;
            if (row.IsDone)
            {
                done++;
                doneBytes += row.Size;
            }
            else if (row.State == ArchiveState.Downloading)
            {
                downloading = true;
                doneBytes += (long) (row.Progress * row.Size);
            }
        }

        IsDownloading = downloading;
        if (total == 0) return string.Empty;

        var text = $"{done:N0} of {total:N0} · {doneBytes.ToFileSizeString()} of {totalBytes.ToFileSizeString()}";
        if (downloading && !string.IsNullOrWhiteSpace(speed))
            text += $" · {speed}";
        return text;
    }
}
