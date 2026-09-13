using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Runs the preflight checklist. Checks run one at a time in <see cref="IPreflightCheck.Order" />;
///     parallelism lives inside a check. Only one run is in flight at a time, and every run gets its own
///     linked cancellation source. Hosts read <see cref="Checks" /> and <see cref="Archives" /> for the
///     current picture and subscribe to <see cref="Changed" /> for transitions; the event is raised from
///     whatever thread made the change.
/// </summary>
public sealed class PreflightRunner
{
    private const int ProgressThrottleMs = 100;

    private readonly object _sync = new();
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly List<Entry> _entries;
    private readonly Dictionary<string, Entry> _byId;
    private readonly ConcurrentDictionary<string, ArchiveStatus> _archives = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _archiveLastEmit = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _runCts;

    public PreflightRunner(IEnumerable<IPreflightCheck> checks, PreflightContext context)
    {
        Context = context;
        _entries = checks
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new Entry(c))
            .ToList();

        _byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            if (!_byId.TryAdd(entry.Check.Id, entry))
                throw new InvalidOperationException($"Duplicate preflight check id '{entry.Check.Id}'");
        }

        foreach (var entry in _entries)
        foreach (var dep in entry.Check.DependsOn)
        {
            if (!_byId.TryGetValue(dep, out var dependency))
                throw new InvalidOperationException(
                    $"Preflight check '{entry.Check.Id}' depends on unknown check '{dep}'");
            if (dependency.Check.Order >= entry.Check.Order)
                throw new InvalidOperationException(
                    $"Preflight check '{entry.Check.Id}' depends on '{dep}', which does not run before it");
        }
    }

    /// <summary>
    ///     Mirrors <c>StandardInstaller.Create</c>: everything but the configuration comes from DI, and the
    ///     checks are whatever <c>IPreflightCheck</c> registrations the host added.
    /// </summary>
    public static PreflightRunner Create(IServiceProvider sp, InstallerConfiguration cfg, PreflightOptions? options = null)
    {
        var context = new PreflightContext(cfg, options ?? new PreflightOptions(),
            sp.GetRequiredService<IGameLocator>(),
            sp.GetRequiredService<FileHashCache>(),
            sp.GetRequiredService<DownloadDispatcher>(),
            sp.GetRequiredService<IResource<DownloadDispatcher>>(),
            sp.GetRequiredService<Client>(),
            sp.GetRequiredService<INexusLoginProbe>(),
            sp.GetRequiredService<IDownloadPolicySource>(),
            sp.GetRequiredService<IManualDownloadAcquirer>(),
            sp.GetRequiredService<IResource<IInstaller>>(),
            sp.GetRequiredService<ILogger<PreflightRunner>>());
        return new PreflightRunner(sp.GetServices<IPreflightCheck>(), context);
    }

    public PreflightContext Context { get; }

    /// <summary>Snapshot of every check, in declared order.</summary>
    public IReadOnlyList<CheckStatus> Checks
    {
        get
        {
            lock (_sync)
            {
                return _entries.Select(e => e.Status).ToArray();
            }
        }
    }

    /// <summary>
    ///     Live view of every archive a check has reported on, keyed by <c>Archive.Name</c>
    ///     (OrdinalIgnoreCase). Values are immutable snapshots.
    /// </summary>
    public IReadOnlyDictionary<string, ArchiveStatus> Archives => _archives;

    public event Action<PreflightEvent>? Changed;

    public PreflightOutcome Outcome
    {
        get
        {
            lock (_sync)
            {
                var checks = _entries.Select(e => e.Status).ToArray();
                return new PreflightOutcome(checks.All(c => c.IsSatisfied), checks);
            }
        }
    }

    /// <summary>
    ///     Runs every check that still needs running: anything not yet Passed, plus anything whose
    ///     dependencies produced a new result since it last passed.
    /// </summary>
    public async Task<PreflightOutcome> RunAll(CancellationToken token)
    {
        try
        {
            await _runLock.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return Outcome;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            lock (_sync)
            {
                _runCts = cts;
            }

            try
            {
                foreach (var entry in _entries)
                {
                    if (cts.IsCancellationRequested) break;
                    if (CanSkip(entry)) continue;
                    await RunEntry(entry, cts.Token);
                }
            }
            finally
            {
                if (cts.IsCancellationRequested) ResetUnfinished();
                lock (_sync)
                {
                    _runCts = null;
                }
            }
        }
        finally
        {
            _runLock.Release();
        }

        var outcome = Outcome;
        Raise(new RunFinished(outcome));
        return outcome;
    }

    /// <summary>
    ///     Re-runs one check. Everything that transitively depends on it goes back to Pending first, so the
    ///     next <see cref="RunAll" /> revisits them.
    /// </summary>
    public async Task<PreflightResult> RunCheck(string id, CancellationToken token)
    {
        var entry = Find(id);

        try
        {
            await _runLock.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return new PreflightResult(PreflightState.Cancelled, "Cancelled");
        }

        PreflightResult result;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            lock (_sync)
            {
                _runCts = cts;
            }

            try
            {
                ResetDependents(entry);
                result = await RunEntry(entry, cts.Token);
            }
            finally
            {
                lock (_sync)
                {
                    _runCts = null;
                }
            }
        }
        finally
        {
            _runLock.Release();
        }

        Raise(new RunFinished(Outcome));
        return result;
    }

    /// <summary>Cancels the run in progress, if any. The running check ends Cancelled.</summary>
    public void Cancel()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            cts = _runCts;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run finished between the read and the cancel.
        }
    }

    /// <summary>Accepts a Warning so it no longer blocks <see cref="PreflightOutcome.Ready" />.</summary>
    public void Acknowledge(string id)
    {
        var entry = Find(id);
        CheckStatus? changed = null;
        lock (_sync)
        {
            if (entry.Status.State == PreflightState.Warning && !entry.Status.Acknowledged)
            {
                entry.Status = entry.Status with {Acknowledged = true};
                changed = entry.Status;
            }
        }

        if (changed != null) Raise(new CheckChanged(changed));
    }

    /// <summary>
    ///     Records a game folder the user picked by hand and sends game-installed and everything downstream
    ///     of it back to Pending. Run the checks again afterwards.
    /// </summary>
    public void SetGameFolder(AbsolutePath folder)
    {
        Context.Config.GameFolder = folder;
        if (!_byId.TryGetValue(PreflightCheckIds.GameInstalled, out var entry)) return;

        var changed = new List<CheckStatus>();
        lock (_sync)
        {
            if (ResetToPending(entry)) changed.Add(entry.Status);
            foreach (var dependent in TransitiveDependents(entry))
            {
                if (ResetToPending(dependent)) changed.Add(dependent.Status);
            }
        }

        foreach (var status in changed) Raise(new CheckChanged(status));
    }

    private Entry Find(string id)
    {
        if (!_byId.TryGetValue(id, out var entry))
            throw new ArgumentException($"No preflight check with id '{id}'", nameof(id));
        return entry;
    }

    private bool CanSkip(Entry entry)
    {
        lock (_sync)
        {
            if (entry.Status.State is not (PreflightState.Passed or PreflightState.Warning)) return false;
            return entry.Check.DependsOn.All(dep =>
                entry.DependencyVersions.TryGetValue(dep, out var seen) && _byId[dep].Version == seen);
        }
    }

    private async Task<PreflightResult> RunEntry(Entry entry, CancellationToken token)
    {
        Entry? blockedBy = null;
        lock (_sync)
        {
            foreach (var dep in entry.Check.DependsOn)
            {
                var dependency = _byId[dep];
                if (dependency.Status.State is PreflightState.Passed or PreflightState.Warning) continue;
                blockedBy = dependency;
                break;
            }
        }

        if (blockedBy != null)
        {
            var skipped = new PreflightResult(PreflightState.Skipped,
                $"Skipped because \"{blockedBy.Check.Title}\" did not pass");
            Complete(entry, skipped);
            return skipped;
        }

        CheckStatus running;
        lock (_sync)
        {
            entry.Status = entry.Status with
            {
                State = PreflightState.Running,
                Message = "Running",
                Detail = null,
                Actions = Array.Empty<PreflightAction>(),
                Progress = Percent.Zero,
                ProgressText = null,
                Acknowledged = false
            };
            entry.LastProgressEmit = 0;
            running = entry.Status;
        }

        Raise(new CheckChanged(running));

        PreflightResult result;
        try
        {
            result = await entry.Check.Run(Context, new ProgressSink(this, entry), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result = new PreflightResult(PreflightState.Cancelled, "Cancelled");
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Preflight check {Check} threw", entry.Check.Id);
            result = new PreflightResult(PreflightState.Failed, $"{entry.Check.Title} failed: {ex.Message}",
                ex.ToString(), new[] {PreflightAction.Retry}, ex);
        }

        if (result.State is PreflightState.Pending or PreflightState.Running)
        {
            result = new PreflightResult(PreflightState.Failed,
                $"{entry.Check.Title} returned an invalid state ({result.State})", result.Detail,
                new[] {PreflightAction.Retry});
        }

        Complete(entry, result);
        return result;
    }

    private void Complete(Entry entry, PreflightResult result)
    {
        CheckStatus status;
        lock (_sync)
        {
            entry.Version++;
            entry.DependencyVersions = entry.Check.DependsOn.ToDictionary(d => d, d => _byId[d].Version);
            entry.Status = entry.Status with
            {
                State = result.State,
                Message = result.Message,
                Detail = result.Detail,
                Actions = result.Actions ?? Array.Empty<PreflightAction>(),
                Progress = result.State == PreflightState.Passed ? Percent.One : entry.Status.Progress,
                ProgressText = null,
                Acknowledged = false
            };
            status = entry.Status;
        }

        Raise(new CheckChanged(status));
    }

    /// <summary>
    ///     After a cancelled run, only what did not get to run goes back to Pending: what was still
    ///     waiting, and what was Skipped because of something upstream. A Failed or NeedsUser result from
    ///     the same run is kept, so the user still sees what to fix.
    /// </summary>
    private void ResetUnfinished()
    {
        var changed = new List<CheckStatus>();
        lock (_sync)
        {
            foreach (var entry in _entries)
            {
                if (entry.Status.State is not (PreflightState.Pending or PreflightState.Running or PreflightState.Skipped))
                    continue;
                if (ResetToPending(entry)) changed.Add(entry.Status);
            }
        }

        foreach (var status in changed) Raise(new CheckChanged(status));
    }

    private void ResetDependents(Entry entry)
    {
        var changed = new List<CheckStatus>();
        lock (_sync)
        {
            foreach (var dependent in TransitiveDependents(entry))
            {
                if (ResetToPending(dependent)) changed.Add(dependent.Status);
            }
        }

        foreach (var status in changed) Raise(new CheckChanged(status));
    }

    /// <summary>Must be called under <see cref="_sync" />. Returns true when the status changed.</summary>
    private static bool ResetToPending(Entry entry)
    {
        if (entry.Status.State is PreflightState.Pending or PreflightState.Running) return false;
        entry.Status = new CheckStatus(entry.Check.Id, entry.Check.Title, PreflightState.Pending, "", null,
            Array.Empty<PreflightAction>(), Percent.Zero, null, false);
        return true;
    }

    /// <summary>Every check downstream of <paramref name="root" />, in declared order.</summary>
    private IEnumerable<Entry> TransitiveDependents(Entry root)
    {
        var affected = new HashSet<string> {root.Check.Id};
        // Dependencies always precede dependents, so one pass in declared order finds the closure.
        foreach (var entry in _entries)
        {
            if (entry == root) continue;
            if (entry.Check.DependsOn.Any(affected.Contains))
                affected.Add(entry.Check.Id);
        }

        return _entries.Where(e => e != root && affected.Contains(e.Check.Id));
    }

    private void ReportProgress(Entry entry, long current, long total, string? text)
    {
        var percent = total > 0 ? Percent.FactoryPutInRange(current, total) : Percent.Zero;
        CheckStatus? snapshot = null;
        lock (_sync)
        {
            if (entry.Status.State != PreflightState.Running) return;
            entry.Status = entry.Status with {Progress = percent, ProgressText = text};
            var now = Environment.TickCount64;
            if (now - entry.LastProgressEmit < ProgressThrottleMs) return;
            entry.LastProgressEmit = now;
            snapshot = entry.Status;
        }

        Raise(new CheckChanged(snapshot));
    }

    private void ReportArchive(Archive archive, ArchiveState state, string? message, long? bytes)
    {
        var key = archive.Name;
        _archives.TryGetValue(key, out var previous);
        var status = new ArchiveStatus(archive, state, message, bytes, previous?.Target);
        _archives[key] = status;

        // Only a progress tick on an unchanged state is throttled; every transition goes out.
        var progressOnly = previous != null && previous.State == state && previous.Message == message;
        if (progressOnly)
        {
            var now = Environment.TickCount64;
            var last = _archiveLastEmit.GetOrAdd(key, 0);
            if (now - last < ProgressThrottleMs) return;
            _archiveLastEmit[key] = now;
        }
        else
        {
            _archiveLastEmit[key] = Environment.TickCount64;
        }

        Raise(new ArchiveChanged(status));
    }

    /// <summary>
    ///     Records the page to open against each queued archive, so a host that only sees
    ///     <see cref="Archives" /> still knows where to send the user, then announces the queue itself.
    /// </summary>
    private void ReportManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue)
    {
        foreach (var (archive, target) in queue)
        {
            var key = archive.Name;
            _archives.TryGetValue(key, out var previous);
            if (previous?.Target == target) continue;
            var status = previous == null
                ? new ArchiveStatus(archive, ArchiveState.ManualRequired, null, null, target)
                : previous with {Target = target};
            _archives[key] = status;
            _archiveLastEmit[key] = Environment.TickCount64;
            Raise(new ArchiveChanged(status));
        }

        Raise(new ManualQueueChanged(queue));
    }

    private void Raise(PreflightEvent evt)
    {
        var handlers = Changed;
        if (handlers == null) return;
        try
        {
            handlers(evt);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "A preflight event handler threw on {Event}", evt.GetType().Name);
        }
    }

    private sealed class Entry
    {
        public Entry(IPreflightCheck check)
        {
            Check = check;
            Status = new CheckStatus(check.Id, check.Title, PreflightState.Pending, "", null,
                Array.Empty<PreflightAction>(), Percent.Zero, null, false);
        }

        public IPreflightCheck Check { get; }
        public CheckStatus Status { get; set; }

        /// <summary>Bumped every time this check produces a result.</summary>
        public int Version { get; set; }

        /// <summary>The <see cref="Version" /> of each dependency when this check last produced a result.</summary>
        public Dictionary<string, int> DependencyVersions { get; set; } = new();

        public long LastProgressEmit { get; set; }
    }

    private sealed class ProgressSink : IPreflightProgress
    {
        private readonly Entry _entry;
        private readonly PreflightRunner _runner;

        public ProgressSink(PreflightRunner runner, Entry entry)
        {
            _runner = runner;
            _entry = entry;
        }

        public void Report(long current, long total, string? text = null)
        {
            _runner.ReportProgress(_entry, current, total, text);
        }

        public void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null)
        {
            _runner.ReportArchive(archive, state, message, bytes);
        }

        public void ManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue)
        {
            _runner.ReportManualQueue(queue);
        }
    }
}
