using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

/// <summary>
///     How to read the card's status line. Declining the login is deliberately not a failure: the user was
///     asked and said no, which is a perfectly good answer and neither a green tick nor a red cross.
/// </summary>
public enum GameFilesTone
{
    Note,
    Working,
    Done,
    Problem
}

/// <summary>
///     The detail panel for the game-files check, when that check has found files it can offer to fetch.
///     <para>
///         The whole thing is an offer and never something that happens to the user. The card says what is
///         wrong, what logging into Steam would buy them, and - because "Wabbajack is going to download game
///         files" invites exactly one question - that the files land in the downloads folder and their game
///         install is never written to. Fixing the install by hand stays an equal way out, which is what
///         Check again is for.
///     </para>
/// </summary>
public partial class GameFilesVM : ViewModel
{
    private readonly Dictionary<string, ArchiveRowVM> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, string, CancellationToken, Task> _execute;
    private readonly ILogger _logger;
    private readonly List<ArchiveRowVM> _rows = new();

    /// <summary>The band headers, and the rows of whichever bands are open, in one list.</summary>
    private readonly ObservableCollection<object> _items = new();

    private readonly PreflightRunner _runner;
    private readonly IServiceProvider _services;

    /// <summary>
    ///     The bands, in the order they are shown, each with what falls in it. Asked in order and first
    ///     match wins, so the last one is "everything else" - a file nobody has touched yet.
    /// </summary>
    private readonly Band[] _bands;

    /// <summary>1 while <see cref="Repair" /> is running, login and all. See the note on that method.</summary>
    private int _inProgress;

    public GameFilesVM(PreflightRunner runner, IServiceProvider services, ILogger logger,
        Func<string, string, CancellationToken, Task> execute, CancellationToken token)
    {
        _runner = runner;
        _services = services;
        _logger = logger;
        _execute = execute;

        Items = new ReadOnlyObservableCollection<object>(_items);

        // Every band shut: a list that takes forty files from the game filled this card with forty rows
        // nobody reads, and what the user wants at a glance is how many are done and how many are not. The
        // download list's bands work the same way, and share the header template.
        _bands = new[]
        {
            new Band(new ArchiveGroupVM("Downloaded", false), r => r.IsDone),
            new Band(new ArchiveGroupVM("Downloading", false), r => r.State == ArchiveState.Downloading),

            // Not "couldn't be fetched": this band is already occupied before anything is fetched, by the
            // files whose copy on disk is from another build of the game.
            new Band(new ArchiveGroupVM("Problems", false),
                r => r.State is ArchiveState.Failed or ArchiveState.Unsupported),
            new Band(new ArchiveGroupVM("Remaining", false), _ => true)
        };

        foreach (var band in _bands)
            band.Header.WhenAnyValue(x => x.IsExpanded)
                .Skip(1)
                .Subscribe(_ => Refresh())
                .DisposeWith(CompositeDisposable);

        HeaderText = "Game files";
        MessageText = string.Empty;
        ExplainText = string.Empty;
        ConsequenceText = string.Empty;
        StatusText = string.Empty;
        ActionLabel = string.Empty;
        SummaryText = string.Empty;

        RepairCommand = ReactiveCommand.CreateFromTask(
            () => _execute(PreflightCheckIds.GameFiles, PreflightAction.RepairGameFiles.Id, token),
            this.WhenAnyValue(x => x.IsRepairing, repairing => !repairing));

        CheckAgainCommand = ReactiveCommand.CreateFromTask(
            () => _execute(PreflightCheckIds.GameFiles, "retry", token),
            this.WhenAnyValue(x => x.IsRepairing, repairing => !repairing));
    }

    /// <summary>
    ///     What the card's list binds to: a header per band that has anything in it, with that band's rows
    ///     after it while it is open. Bands with nothing in them are left out rather than shown empty.
    /// </summary>
    public ReadOnlyObservableCollection<object> Items { get; }

    [Reactive] public partial string HeaderText { get; set; }

    /// <summary>Count and total size of what is on the card, for the right of its header.</summary>
    [Reactive] public partial string SummaryText { get; set; }

    /// <summary>What the check said. The panel's own message is hidden while this card is up.</summary>
    [Reactive] public partial string MessageText { get; set; }

    /// <summary>What fetching would do, or what logging in would buy - the restorer's own words.</summary>
    [Reactive] public partial string ExplainText { get; set; }

    /// <summary>
    ///     What the repair would do beyond downloading. Empty almost always; when it is not, it is the one
    ///     thing here that changes something outside the downloads folder - a free tool licence taken on the
    ///     user's Steam account - so it is said in the offer, in its own line, before they agree to it.
    /// </summary>
    [Reactive] public partial string ConsequenceText { get; set; }

    [Reactive] public partial string StatusText { get; set; }

    /// <summary>How to read <see cref="StatusText" />. Drives the icon beside it, and the card's border.</summary>
    [Reactive] public partial GameFilesTone Tone { get; set; }

    [Reactive] public partial bool IsRepairing { get; set; }
    [Reactive] public partial string ActionLabel { get; set; }

    /// <summary>True when the user is not logged in yet, so the button can say so.</summary>
    [Reactive] public partial bool NeedsLogin { get; set; }

    public ReactiveCommand<Unit, Unit> RepairCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckAgainCommand { get; }

    /// <summary>
    ///     Re-reads the check's finding. Called on the UI thread whenever the check reports, so the list is
    ///     rebuilt from a <c>RepairableGameFiles</c> the check has finished writing.
    /// </summary>
    public void Apply(CheckStatus status)
    {
        MessageText = status.Message;

        var repairable = _runner.Context.State.RepairableGameFiles;

        // Mid-repair the check is not running, so anything arriving here is a re-run that has replaced the
        // list; either way the rows follow it.
        if (!RowsMatch(repairable)) Rebuild(repairable);

        SummaryText = repairable.Count == 0
            ? string.Empty
            : $"{Files(repairable.Count)} · {repairable.Sum(r => r.Archive.Size).ToFileSizeString()}";

        Describe();
    }

    /// <summary>
    ///     Logs in if it has to, fetches, then asks the check again. Called by
    ///     <see cref="PreflightActionDispatcher" /> for the repair-game-files action.
    ///     <para>
    ///         The guard is on the operation and not on the button, because there is more than one button.
    ///         The card's own command is gated on <see cref="IsRepairing" />, but the checklist row offers
    ///         the same action and knows nothing about it - the game-files <em>check</em> is not running
    ///         during a repair, so that row stays live - and pressing it twice would have two
    ///         <c>GameFileRepair.Run</c> passes writing the same output paths at once.
    ///     </para>
    /// </summary>
    public async Task Repair(CancellationToken token)
    {
        // Covers the whole thing, the login included: a second press while the login pane is up would
        // otherwise be let through, since nothing is being fetched yet.
        if (Interlocked.Exchange(ref _inProgress, 1) == 1)
        {
            _logger.LogInformation("A game file repair is already under way");
            return;
        }

        try
        {
            IsRepairing = true;
            await RepairCore(token);
        }
        finally
        {
            IsRepairing = false;
            Interlocked.Exchange(ref _inProgress, 0);
        }
    }

    private async Task RepairCore(CancellationToken token)
    {
        var ctx = _runner.Context;
        var items = ctx.State.RepairableGameFiles;
        if (items.Count == 0) return;

        var restorer = ctx.GameFileRestorer;
        if (restorer == null)
        {
            Fail("This build of Wabbajack has no way to fetch game files.");
            return;
        }

        // Asked again after the login, because logging in is the thing that changes the answer.
        if (!restorer.Status().Ready && !await LogIn(restorer.SourceName, token)) return;

        var status = restorer.Status();
        if (!status.Ready)
        {
            Fail(status.Reason);
            return;
        }

        Tone = GameFilesTone.Working;
        StatusText = $"Fetching from {restorer.SourceName}";

        IReadOnlyList<GameFileRepairResult> results;
        try
        {
            results = await GameFileRepair.Run(ctx, items, new Sink(this), token);
        }
        catch (OperationCanceledException)
        {
            Tone = GameFilesTone.Note;
            StatusText = "Stopped.";
            return;
        }

        Summarise(results);

        // The check owns whether this run can carry on, so it is the one that decides. A pass from here
        // releases the rest of the checklist, which is what the user was after.
        await _execute(PreflightCheckIds.GameFiles, "retry", token);
    }

    /// <summary>
    ///     Puts the login pane in front of the user and waits. False when they closed it without logging in,
    ///     which is a perfectly good answer: their account is theirs to hand over or not.
    /// </summary>
    private async Task<bool> LogIn(string sourceName, CancellationToken token)
    {
        var pane = _services.GetRequiredService<SteamLoginVM>();
        try
        {
            Tone = GameFilesTone.Working;
            StatusText = $"Waiting for you to log into {sourceName}";
            await using var registration = token.Register(() => pane.CloseCommand.Execute(null));

            if (await ShowSteamLogin.Send(pane)) return true;

            // Closing the pane is an answer, not a failure: the account is theirs to hand over or not, and
            // the offer is still there if they change their mind.
            Tone = GameFilesTone.Note;
            StatusText = $"Not logged into {sourceName}, so nothing has been fetched.";
            return false;
        }
        finally
        {
            pane.Dispose();
        }
    }

    /// <summary>
    ///     What the button offers and what the card says it would do. Both follow whether anyone is logged
    ///     in, which is why the offer stands either way: a user who is not has to be told what logging in
    ///     would get them and then decide.
    /// </summary>
    private void Describe()
    {
        var restorer = _runner.Context.GameFileRestorer;
        if (restorer == null)
        {
            NeedsLogin = false;
            ActionLabel = string.Empty;
            ExplainText = string.Empty;
            ConsequenceText = string.Empty;
            return;
        }

        var status = restorer.Status();
        NeedsLogin = !status.Ready;
        ActionLabel = status.Ready ? $"Fetch from {restorer.SourceName}" : $"Log into {restorer.SourceName}";
        ExplainText = status.Ready
            ? $"{restorer.SourceName} can fetch these into your downloads folder, which is all the install " +
              "needs. Your game install is never written to, and the files can be deleted again afterwards."
            : status.Reason;

        // Asked about the games actually on the card, so it appears when it is true and stays away when it
        // is not: a warning shown on every repair is a warning nobody reads by the time it matters.
        ConsequenceText = string.Join(" ",
            restorer.Consequences(_runner.Context.State.RepairableGameFiles.Select(r => r.State.Game)));
    }

    private bool RowsMatch(IReadOnlyList<RepairableGameFile> repairable)
    {
        return repairable.Count == _rows.Count &&
               repairable.All(r => _byName.ContainsKey(r.Archive.Name));
    }

    private void Rebuild(IReadOnlyList<RepairableGameFile> repairable)
    {
        _rows.Clear();
        _byName.Clear();
        StatusText = string.Empty;
        Tone = GameFilesTone.Note;

        foreach (var item in GameFileRepair.Group(repairable))
        {
            var row = new ArchiveRowVM(item.Archive, null)
            {
                // A file of a game that is not installed reads as one nobody has fetched yet rather than as
                // a failure: nothing has gone wrong with it, and the repair has not been asked for.
                State = item.Problem == GameFileProblem.Missing ? ArchiveState.Missing : ArchiveState.Failed,
                Detail = item.Problem == GameFileProblem.Missing
                    ? "Not installed"
                    : item.Version is {} version
                        ? $"Another version; this list needs {version}"
                        : "Another version"
            };

            _rows.Add(row);
            _byName[row.Key] = row;
        }

        Refresh();
    }

    /// <summary>
    ///     Re-bands the rows and redraws the list. Cheap by design: a repair touches tens of files, not the
    ///     thousands the download list handles, and each one reports twice - once when it starts and once
    ///     when it is done - so this runs a few dozen times over a whole repair.
    /// </summary>
    private void Refresh()
    {
        var flat = new List<object>();

        foreach (var band in _bands)
        {
            var rows = _rows.Where(r => BandOf(r) == band).ToList();
            band.Header.Summarize(rows.Count, rows.Sum(r => r.Size));
            if (rows.Count == 0) continue;

            flat.Add(band.Header);
            if (band.Header.IsExpanded) flat.AddRange(rows);
        }

        _items.Clear();
        foreach (var item in flat) _items.Add(item);
    }

    private Band BandOf(ArchiveRowVM row)
    {
        return _bands.First(b => b.Holds(row));
    }

    /// <summary>
    ///     One line for a whole repair. Failures are counted by their message rather than reported file by
    ///     file, because they almost never differ: a list built against a game version nobody indexed fails
    ///     every one of its files with the same sentence, and naming one of them made that read like a
    ///     problem with that file. The most common reason is the one shown, with the rest counted.
    /// </summary>
    private void Summarise(IReadOnlyList<GameFileRepairResult> results)
    {
        var fetched = results.Count(r => r.Status == GameFileRepairStatus.Repaired);

        if (fetched == results.Count)
        {
            Tone = GameFilesTone.Done;
            StatusText = $"{Files(fetched)} fetched into the downloads folder.";
            return;
        }

        var failed = results.Where(r => r.Status != GameFileRepairStatus.Repaired).ToList();
        var reasons = failed.GroupBy(r => r.Message, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ToList();
        var commonest = reasons[0];

        var what = fetched == 0
            ? $"{Files(failed.Count)} could not be fetched."
            : $"{fetched} of {results.Count} fetched; {Files(failed.Count)} could not be.";

        // Whether the reason belongs to all of them, or to most: a user reading one sentence should know
        // how much of the list it accounts for.
        var scope = reasons.Count == 1
            ? string.Empty
            : $" ({commonest.Count()} of {failed.Count})";

        Tone = GameFilesTone.Problem;
        StatusText = $"{what}{scope} {commonest.Key}";
    }

    private static string Files(int count)
    {
        return $"{count} {(count == 1 ? "file" : "files")}";
    }

    private void Fail(string message)
    {
        Tone = GameFilesTone.Problem;
        StatusText = message;
    }

    /// <summary>One band of the card's list: a header the user can open, and what falls in it.</summary>
    private sealed record Band(ArchiveGroupVM Header, Func<ArchiveRowVM, bool> Holds);

    /// <summary>
    ///     Where the repair reports to. It runs outside a check, so there is no runner sink to forward to and
    ///     this drives the card directly; every method can be called from any thread, so every one of them
    ///     hops to the UI before touching anything bound.
    /// </summary>
    private sealed class Sink : IPreflightProgress
    {
        private readonly GameFilesVM _owner;

        public Sink(GameFilesVM owner)
        {
            _owner = owner;
        }

        public void Report(long current, long total, string? text = null)
        {
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                if (!string.IsNullOrWhiteSpace(text)) _owner.StatusText = text;
            });
        }

        public void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null)
        {
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                if (!_owner._byName.TryGetValue(archive.Name, out var row)) return;

                row.Apply(new ArchiveStatus(archive, state, message, bytes, null));

                // The row has very likely moved band - that is what this report is - so the counts and the
                // list follow it.
                _owner.Refresh();
            });
        }

        public void ManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue)
        {
            // Game files are never routed to a browser: no page sells you an old build.
        }
    }
}
