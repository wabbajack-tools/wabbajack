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
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.Installer.Preflight;
using Wabbajack.App.Avalonia.LoginManagers;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

/// <summary>
///     Hosts one <see cref="PreflightRunner" /> for the page between the folder picker and the install. The
///     runner raises plain events from any thread; this bridges them onto the UI thread and projects them onto
///     the checklist, the detail panel and the title-bar progress. The decisions (what to run, when a check is
///     satisfied) stay in the runner.
/// </summary>
public partial class PreflightVM : ViewModel
{
    private readonly PreflightRunner _runner;
    private readonly ProgressViewModel _progressHost;
    private readonly ILogger _logger;
    private readonly PreflightActionDispatcher _actions;
    private readonly CancellationTokenSource _cts = new();
    private readonly ObservableCollection<PreflightCheckVM> _checks = new();
    private readonly Dictionary<string, PreflightCheckVM> _byId = new(StringComparer.Ordinal);
    private readonly List<Task> _runs = new();
    private Task? _shutdown;
    private Task? _watcherStopped;
    private int _runsInFlight;
    private bool _disposed;

    public PreflightVM(PreflightRunner runner, NexusLoginManager nexusLogin, ProgressViewModel progressHost,
        ILogger logger, IServiceProvider serviceProvider, IObservable<string> downloadSpeed, ICommand openReadme,
        ICommand openWebsite, ICommand openCommunity, ICommand openManifest)
    {
        _runner = runner;
        _progressHost = progressHost;
        _logger = logger;

        // Built before the dispatcher, which routes the repair action to it. Its own buttons go back out
        // through ExecuteAction, so both ways of asking for the same thing take the same path.
        GameFiles = new GameFilesVM(runner, serviceProvider, logger, ExecuteAction, _cts.Token);
        _actions = new PreflightActionDispatcher(runner, nexusLogin, GameFiles, logger);

        OpenReadmeCommand = openReadme;
        OpenWebsiteCommand = openWebsite;
        OpenCommunityCommand = openCommunity;
        OpenManifestCommand = openManifest;
        SummaryText = string.Empty;

        foreach (var status in runner.Checks)
        {
            var check = new PreflightCheckVM(status, ExecuteAction, _cts.Token);
            _checks.Add(check);
            _byId[check.Id] = check;
        }

        Checks = new ReadOnlyObservableCollection<PreflightCheckVM>(_checks);

        var events = Observable
            .FromEvent<PreflightEvent>(h => runner.Changed += h, h => runner.Changed -= h)
            .Publish();

        BulkDownloads = new BulkDownloadsVM(runner, events.OfType<ArchiveChanged>().Select(e => e.Status), downloadSpeed);
        ManualDownloads = new ManualDownloadsVM(runner.Context.Acquirer, events.OfType<ManualQueueChanged>(),
            _cts.Token, logger);

        events.OfType<CheckChanged>()
            .ObserveOnGuiThread()
            .Subscribe(e =>
            {
                if (_byId.TryGetValue(e.Status.Id, out var check)) check.Apply(e.Status);
                if (e.Status.Id == PreflightCheckIds.GameFiles) GameFiles.Apply(e.Status);
                Recompute();
            })
            .DisposeWith(CompositeDisposable);

        events.OfType<RunFinished>()
            .ObserveOnGuiThread()
            .Subscribe(_ => Recompute())
            .DisposeWith(CompositeDisposable);

        events.Connect().DisposeWith(CompositeDisposable);

        // Both a completed login and a window closed without one land here; the check decides which it was.
        // It follows the refresh rather than LoggedIn because a credential this machine holds can have been
        // revoked at the other end, which LoggedIn cannot see: it says a usable credential is stored, while
        // the check asks Nexus what it is worth. Logging in again over one of those leaves LoggedIn true
        // throughout, so watching it would miss the login that fixed the row.
        nexusLogin.Refreshed
            .ObserveOnGuiThread()
            // Only once the checklist has actually asked. nexus-login runs after the archive inventory now,
            // so re-running it while that is still Pending would mark it Skipped over a login the user has
            // just completed; the run that reaches it asks again anyway.
            .Where(_ => _runner.Checks.Any(c =>
                c.Id == PreflightCheckIds.NexusLogin && c.State != PreflightState.Pending))
            .Subscribe(_ => Run(t => _actions.Execute(PreflightCheckIds.NexusLogin, "retry", t)).FireAndForget())
            .DisposeWith(CompositeDisposable);

        InstallCommand = ReactiveCommand.Create(() => { },
            this.WhenAnyValue(x => x.AllPassed, x => x.IsRunning, (passed, running) => passed && !running));
        BackCommand = ReactiveCommand.Create(() => { });

        Recompute();
    }

    public ReadOnlyObservableCollection<PreflightCheckVM> Checks { get; }
    public BulkDownloadsVM BulkDownloads { get; }
    public ManualDownloadsVM ManualDownloads { get; }
    public GameFilesVM GameFiles { get; }

    [Reactive] public partial PreflightCheckVM? ActiveCheck { get; set; }
    [Reactive] public partial PreflightDetailKind DetailKind { get; set; }
    [Reactive] public partial bool AllPassed { get; set; }
    [Reactive] public partial bool IsRunning { get; set; }
    [Reactive] public partial int PassedCount { get; set; }
    [Reactive] public partial int TotalCount { get; set; }
    [Reactive] public partial Percent OverallPercent { get; set; }
    [Reactive] public partial string SummaryText { get; set; }

    /// <summary>Empty on purpose: the owner subscribes and starts the install.</summary>
    public ReactiveCommand<Unit, Unit> InstallCommand { get; }

    /// <summary>Empty on purpose: the owner subscribes and returns to the folder page.</summary>
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public ICommand OpenReadmeCommand { get; }
    public ICommand OpenWebsiteCommand { get; }
    public ICommand OpenCommunityCommand { get; }
    public ICommand OpenManifestCommand { get; }

    /// <summary>Runs every check that has not passed yet. Safe to call from the UI thread; returns at once.</summary>
    public void Start()
    {
        Run(t => _runner.RunAll(t)).FireAndForget();
    }

    /// <summary>Stops the run in progress and anything the page kicked off. The checks that finished keep their result.</summary>
    public void Cancel()
    {
        _runner.Cancel();
        _cts.Cancel();
    }

    /// <summary>Awaits whatever run is in flight, so a caller can hand the folders to the installer safely.</summary>
    public async Task WaitForIdle()
    {
        Task[] runs;
        lock (_runs)
        {
            runs = _runs.ToArray();
        }

        try
        {
            await Task.WhenAll(runs);
        }
        catch (Exception)
        {
            // Each run logs its own failure.
        }
    }

    private Task ExecuteAction(string checkId, string actionId, CancellationToken token)
    {
        return Run(t => _actions.Execute(checkId, actionId, t));
    }

    /// <summary>Every call into the runner goes through here so <see cref="IsRunning" /> covers the whole call.</summary>
    private async Task Run(Func<CancellationToken, Task> operation)
    {
        if (_disposed || _cts.IsCancellationRequested) return;

        Interlocked.Increment(ref _runsInFlight);
        Recompute();
        var task = operation(_cts.Token);
        lock (_runs)
        {
            _runs.RemoveAll(t => t.IsCompleted);
            _runs.Add(task);
        }

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preflight failed unexpectedly");
        }
        finally
        {
            Interlocked.Decrement(ref _runsInFlight);
            RxApp.MainThreadScheduler.Schedule(Recompute);
        }
    }

    private void Recompute()
    {
        if (_disposed) return;

        TotalCount = _checks.Count;
        PassedCount = _checks.Count(c => c.IsSatisfied);
        IsRunning = Volatile.Read(ref _runsInFlight) > 0 || _checks.Any(c => c.State == PreflightState.Running);
        AllPassed = _runner.Outcome.Ready;
        OverallPercent = TotalCount == 0 ? Percent.Zero : Percent.FactoryPutInRange(PassedCount, TotalCount);
        SummaryText = $"{PassedCount} of {TotalCount} checks passed";

        var active = _checks.FirstOrDefault(c => c.State == PreflightState.Running)
                     ?? _checks.FirstOrDefault(c => c.State is PreflightState.NeedsUser or PreflightState.Failed
                                                        or PreflightState.Cancelled
                                                    || (c.State == PreflightState.Warning && !c.Acknowledged));
        foreach (var check in _checks) check.IsActive = ReferenceEquals(check, active);
        ActiveCheck = active;
        DetailKind = active?.DetailKind ?? PreflightDetailKind.None;

        _progressHost.ProgressText = $"Preflight: {SummaryText}";
        _progressHost.ProgressPercent = OverallPercent;
    }

    /// <summary>
    ///     Cancels the run, releases the subscriptions and shuts the acquirer down. The returned task completes
    ///     once the run has unwound and the acquirer has stopped moving files, which is what the installer
    ///     needs before it hashes the downloads folder. Idempotent.
    /// </summary>
    public Task ShutdownAsync()
    {
        lock (_runs)
        {
            return _shutdown ??= ShutdownCore();
        }
    }

    /// <summary>
    ///     Starts the shutdown if it has not started yet, and completes once the manual download watcher has
    ///     stopped, so nothing is left half-copied in the downloads folder. The watcher places files on its
    ///     own threads, which is why this finishes even when the thread that called it is then blocked; the
    ///     rest of the shutdown may still be unwinding. Idempotent.
    /// </summary>
    public Task StopWatcherAsync()
    {
        ShutdownAsync().FireAndForget();
        lock (_runs)
        {
            return _watcherStopped ?? Task.CompletedTask;
        }
    }

    private async Task ShutdownCore()
    {
        _disposed = true;

        // First, and off this thread on purpose: the watcher unwinds on its own threads, so it still
        // finishes for a caller that blocks waiting on the shutdown, and the runs it no longer waits on
        // cannot hold it up. Starting it before the disposals below means a throw in one of them still
        // leaves a closing window something to wait on.
        _watcherStopped = Task.Run(StopWatcher);

        Cancel();
        base.Dispose();
        BulkDownloads.Dispose();
        ManualDownloads.Dispose();
        GameFiles.Dispose();
        foreach (var check in _checks) check.Dispose();

        // The title bar carries the checklist's own progress, so it goes when the page does. Recompute is
        // already shut out by _disposed, and every caller gets here before it writes a title of its own.
        _progressHost.ProgressText = string.Empty;
        _progressHost.ProgressPercent = Percent.Zero;

        await WaitForIdle();
        await _watcherStopped;
    }

    private async Task StopWatcher()
    {
        try
        {
            await _runner.Context.Acquirer.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "While shutting down the manual download watcher");
        }
    }

    public override void Dispose()
    {
        ShutdownAsync().FireAndForget();
    }
}
