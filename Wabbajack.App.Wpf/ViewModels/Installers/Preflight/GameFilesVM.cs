using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
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
using Wabbajack.Messages;
using Wabbajack.RateLimiter;

namespace Wabbajack;

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
    private readonly ObservableCollection<ArchiveRowVM> _rows = new();
    private readonly PreflightRunner _runner;
    private readonly IServiceProvider _services;

    public GameFilesVM(PreflightRunner runner, IServiceProvider services, ILogger logger,
        Func<string, string, CancellationToken, Task> execute, CancellationToken token)
    {
        _runner = runner;
        _services = services;
        _logger = logger;
        _execute = execute;

        Rows = new ReadOnlyObservableCollection<ArchiveRowVM>(_rows);
        HeaderText = "Game files";
        MessageText = string.Empty;
        ExplainText = string.Empty;
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

    public ReadOnlyObservableCollection<ArchiveRowVM> Rows { get; }

    [Reactive] public partial string HeaderText { get; set; }

    /// <summary>Count and total size of what is on the card, for the right of its header.</summary>
    [Reactive] public partial string SummaryText { get; set; }

    /// <summary>What the check said. The panel's own message is hidden while this card is up.</summary>
    [Reactive] public partial string MessageText { get; set; }

    /// <summary>What fetching would do, or what logging in would buy - the restorer's own words.</summary>
    [Reactive] public partial string ExplainText { get; set; }

    [Reactive] public partial string StatusText { get; set; }

    /// <summary>True once something has gone wrong, so the card's border says so.</summary>
    [Reactive] public partial bool Failed { get; set; }

    [Reactive] public partial bool IsRepairing { get; set; }
    [Reactive] public partial Percent Progress { get; set; }
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
    ///     <see cref="PreflightActionDispatcher" /> for the repair-game-files action, on the UI thread.
    /// </summary>
    public async Task Repair(CancellationToken token)
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

        IsRepairing = true;
        Failed = false;
        Progress = Percent.Zero;
        StatusText = $"Fetching from {restorer.SourceName}";

        IReadOnlyList<GameFileRepairResult> results;
        try
        {
            results = await GameFileRepair.Run(ctx, items, new Sink(this), token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Stopped.";
            IsRepairing = false;
            return;
        }
        finally
        {
            IsRepairing = false;
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
            StatusText = $"Waiting for you to log into {sourceName}";
            await using var registration = token.Register(() => pane.CloseCommand.Execute(null));

            if (await ShowSteamLogin.Send(pane)) return true;

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
            return;
        }

        var status = restorer.Status();
        NeedsLogin = !status.Ready;
        ActionLabel = status.Ready ? $"Fetch from {restorer.SourceName}" : $"Log into {restorer.SourceName}";
        ExplainText = status.Ready
            ? $"{restorer.SourceName} can fetch these into your downloads folder, which is all the install " +
              "needs. Your game install is never written to, and the files can be deleted again afterwards."
            : status.Reason;
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
        Failed = false;
        Progress = Percent.Zero;

        foreach (var item in GameFileRepair.Group(repairable))
        {
            var row = new ArchiveRowVM(item.Archive, null)
            {
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
    }

    private void Summarise(IReadOnlyList<GameFileRepairResult> results)
    {
        var fetched = results.Count(r => r.Status == GameFileRepairStatus.Repaired);
        Progress = Percent.One;

        if (fetched == results.Count)
        {
            Failed = false;
            StatusText = $"{Files(fetched)} fetched into the downloads folder.";
            return;
        }

        // The first thing that did not work, in the repair's own words: it knows the difference between a
        // version nobody wrote down and a file the depot does not carry, and the user can act on that.
        var first = results.First(r => r.Status != GameFileRepairStatus.Repaired);
        Failed = true;
        StatusText = fetched == 0
            ? first.Message
            : $"{fetched} of {results.Count} fetched. {first.Archive.Name}: {first.Message}";
    }

    private static string Files(int count)
    {
        return $"{count} {(count == 1 ? "file" : "files")}";
    }

    private void Fail(string message)
    {
        Failed = true;
        StatusText = message;
    }

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
                _owner.Progress = total <= 0 ? Percent.Zero : Percent.FactoryPutInRange(current, total);
                if (!string.IsNullOrWhiteSpace(text)) _owner.StatusText = text;
            });
        }

        public void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null)
        {
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                if (_owner._byName.TryGetValue(archive.Name, out var row))
                    row.Apply(new ArchiveStatus(archive, state, message, bytes, null));
            });
        }

        public void ManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue)
        {
            // Game files are never routed to a browser: no page sells you an old build.
        }
    }
}
