using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack;

/// <summary>
///     The guided walk through the archives no automated source could fetch: one card for the file the user
///     should get next, driven by the acquirer that watches their Downloads folder, plus the full queue as a
///     list. The acquirer owns the queue and its order; this only projects it.
/// </summary>
public partial class ManualDownloadsVM : ViewModel
{
    private static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan DoneBeat = TimeSpan.FromMilliseconds(1200);

    private static readonly IComparer<ArchiveRowVM> QueueOrder = Comparer<ArchiveRowVM>.Create((a, b) =>
    {
        var done = a.IsDone.CompareTo(b.IsDone);
        if (done != 0) return done;
        var order = a.Order.CompareTo(b.Order);
        return order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    });

    private readonly IManualDownloadAcquirer _acquirer;
    private readonly ILogger _logger;
    private readonly CancellationToken _token;
    private readonly SourceCache<ManualDownloadItem, string> _items = new(i => i.Key);
    private readonly Dictionary<string, ArchiveRowVM> _rowsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReadOnlyObservableCollection<ArchiveRowVM> _rows;
    private Dictionary<string, ManualDownloadTarget> _targets = new(StringComparer.OrdinalIgnoreCase);
    private string? _shownKey;
    private bool _holdingDone;

    public ManualDownloadsVM(IManualDownloadAcquirer acquirer, IObservable<ManualQueueChanged> queue,
        CancellationToken token, ILogger logger)
    {
        _acquirer = acquirer;
        _logger = logger;
        _token = token;

        WatchStatusText = string.Empty;
        Instructions = string.Empty;
        SiteName = string.Empty;
        CurrentSizeText = string.Empty;
        NoticeText = string.Empty;
        WatchFolderText = string.Empty;
        HeaderText = "Manual downloads";

        WatchFolderPicker = new FilePickerVM
        {
            PathType = FilePickerVM.PathTypeOptions.Folder,
            ExistCheckOption = FilePickerVM.CheckOptions.On,
            PromptTitle = "Choose the folder your browser saves downloads to"
        };

        _items.Connect()
            .TransformWithInlineUpdate(item =>
            {
                var row = new ArchiveRowVM(item.Archive, Target(item.Key));
                row.Apply(item);
                _rowsByKey[item.Key] = row;
                return row;
            }, (row, item) => row.Apply(item))
            .SortAndBind(out _rows, QueueOrder)
            .Subscribe()
            .DisposeWith(CompositeDisposable);

        queue.ObserveOnGuiThread()
            .Subscribe(q =>
            {
                _targets = q.Queue.ToDictionary(e => e.Archive.Name, e => e.Target, StringComparer.OrdinalIgnoreCase);
                Reload();
            })
            .DisposeWith(CompositeDisposable);

        acquirer.Events
            .Buffer(BatchWindow)
            .Where(batch => batch.Count > 0)
            .ObserveOnGuiThread()
            .Subscribe(OnEvents)
            .DisposeWith(CompositeDisposable);

        acquirer.Notices
            .ObserveOnGuiThread()
            .Subscribe(notice =>
            {
                NoticeText = notice.Kind == ManualDownloadNoticeKind.WatchFolderChanged ? string.Empty : notice.Message;
                RefreshWatchFolder();
            })
            .DisposeWith(CompositeDisposable);

        WatchFolderPicker.WhenAnyValue(x => x.TargetPath)
            .Skip(1)
            .Where(p => p != default && p.DirectoryExists())
            .Subscribe(p => acquirer.SetWatchFolder(p))
            .DisposeWith(CompositeDisposable);

        var hasCurrent = this.WhenAnyValue(x => x.HasCurrent);
        OpenPageCommand = ReactiveCommand.Create(() =>
        {
            if (Current?.Url is { } url) UIUtils.OpenWebsite(url);
        }, this.WhenAnyValue(x => x.Current).Select(c => c?.Url != null));

        SkipCommand = ReactiveCommand.Create(() =>
        {
            if (Current is { } current) acquirer.Skip(current.Key);
        }, hasCurrent);

        PickFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (Current is not { } current) return;
            var initial = acquirer.WatchFolder == default ? null : acquirer.WatchFolder.ToString();
            var file = UIUtils.OpenFileDialog("All files|*.*", initial);
            if (file == default) return;
            try
            {
                await acquirer.AddFileManually(current.Key, file, _token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While checking {File} for {Archive}", file, current.Key);
            }
        }, hasCurrent);

        RetryCommand = ReactiveCommand.Create(() =>
        {
            if (Current is { } current) acquirer.Retry(current.Key);
        }, this.WhenAnyValue(x => x.WatchState)
            .Select(s => s is ManualDownloadState.WrongFile or ManualDownloadState.Failed));

        ToggleShowAllCommand = ReactiveCommand.Create(() => { ShowAll = !ShowAll; });

        Reload();
    }

    public ReadOnlyObservableCollection<ArchiveRowVM> Rows => _rows;
    public FilePickerVM WatchFolderPicker { get; }

    /// <summary>The row for the file the user should fetch next; the same instance the list shows.</summary>
    [Reactive] public partial ArchiveRowVM? Current { get; set; }

    [Reactive] public partial bool HasCurrent { get; set; }
    [Reactive] public partial int CurrentIndex { get; set; }
    [Reactive] public partial int TotalCount { get; set; }
    [Reactive] public partial int MovedCount { get; set; }
    [Reactive] public partial string HeaderText { get; set; }
    [Reactive] public partial ManualDownloadState WatchState { get; set; }
    [Reactive] public partial string WatchStatusText { get; set; }
    [Reactive] public partial bool IsWrongFile { get; set; }
    [Reactive] public partial string Instructions { get; set; }
    [Reactive] public partial string SiteName { get; set; }
    [Reactive] public partial string CurrentSizeText { get; set; }
    [Reactive] public partial string NoticeText { get; set; }
    [Reactive] public partial string WatchFolderText { get; set; }
    [Reactive] public partial bool ShowAll { get; set; }

    public ReactiveCommand<Unit, Unit> OpenPageCommand { get; }
    public ReactiveCommand<Unit, Unit> SkipCommand { get; }
    public ReactiveCommand<Unit, Unit> PickFileCommand { get; }
    public ReactiveCommand<Unit, Unit> RetryCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleShowAllCommand { get; }

    private ManualDownloadTarget? Target(string key)
    {
        return _targets.TryGetValue(key, out var target) ? target : null;
    }

    private void OnEvents(IList<ManualDownloadEvent> batch)
    {
        Reload();

        // Let "Done" sit on screen for a beat before the card moves on to the next file.
        if (!_holdingDone && _shownKey != null &&
            batch.Any(e => e.Item.State == ManualDownloadState.Moved &&
                           string.Equals(e.Item.Key, _shownKey, StringComparison.OrdinalIgnoreCase)))
        {
            _holdingDone = true;
            WatchState = ManualDownloadState.Moved;
            IsWrongFile = false;
            WatchStatusText = $"Done, {_shownKey} is in place";
            Observable.Timer(DoneBeat)
                .ObserveOnGuiThread()
                .Subscribe(_ =>
                {
                    _holdingDone = false;
                    RefreshCurrent();
                })
                .DisposeWith(CompositeDisposable);
            return;
        }

        if (!_holdingDone) RefreshCurrent();
    }

    /// <summary>Brings the list in line with the acquirer; keys are stable so rows keep their instances.</summary>
    private void Reload()
    {
        var snapshot = _acquirer.Snapshot();
        _items.Edit(cache =>
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in snapshot)
            {
                seen.Add(item.Key);
                cache.AddOrUpdate(item);
            }

            foreach (var key in cache.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                cache.Remove(key);
                _rowsByKey.Remove(key);
            }
        });

        var counts = _acquirer.Counts;
        TotalCount = counts.Total;
        MovedCount = counts.Moved;
        RefreshWatchFolder();
        if (!_holdingDone) RefreshCurrent();
    }

    private void RefreshWatchFolder()
    {
        var folder = _acquirer.WatchFolder;
        if (folder == default) return;
        WatchFolderText = folder.ToString();
        if (WatchFolderPicker.TargetPath == default) WatchFolderPicker.TargetPath = folder;
    }

    private void RefreshCurrent()
    {
        var item = _acquirer.Current;
        if (item == null)
        {
            _shownKey = null;
            Current = null;
            HasCurrent = false;
            IsWrongFile = false;
            WatchState = ManualDownloadState.Moved;
            HeaderText = TotalCount == 0 ? "Manual downloads" : $"All {TotalCount} files are in place";
            WatchStatusText = string.Empty;
            Instructions = string.Empty;
            SiteName = string.Empty;
            CurrentSizeText = string.Empty;
            return;
        }

        _shownKey = item.Key;
        Current = _rowsByKey.TryGetValue(item.Key, out var row) ? row : null;
        HasCurrent = Current != null;
        CurrentIndex = Math.Min(MovedCount + 1, Math.Max(TotalCount, 1));
        HeaderText = $"Manual download {CurrentIndex} of {TotalCount}";
        CurrentSizeText = item.Archive.Size.ToFileSizeString();

        var target = Target(item.Key);
        SiteName = target?.SiteName ?? string.Empty;
        Instructions = target?.Instructions ?? string.Empty;

        WatchState = item.State;
        IsWrongFile = item.State == ManualDownloadState.WrongFile;
        WatchStatusText = Describe(item);
    }

    private string Describe(ManualDownloadItem item)
    {
        var candidate = item.CandidatePath is { } path ? path.FileName.ToString() : item.Key;
        return item.State switch
        {
            ManualDownloadState.Pending => "Watching your Downloads folder for this file",
            ManualDownloadState.Detected => $"Found {candidate}, waiting for the download to finish",
            ManualDownloadState.Waiting => $"Found {candidate}, your browser is still writing it",
            ManualDownloadState.Verifying => $"Verifying {candidate} ({(int) (item.Progress.Value * 100)}%)",
            ManualDownloadState.Moved => $"Done, {item.Key} is in place",
            ManualDownloadState.WrongFile => "That file doesn't match, wrong version? " + (item.Message ?? string.Empty),
            ManualDownloadState.Failed => item.Message ?? "Could not put the file in place",
            _ => item.Message ?? string.Empty
        };
    }
}
