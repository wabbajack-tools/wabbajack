#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class PreflightRunnerTests : IDisposable
{
    private readonly PreflightTestHost _host;

    public PreflightRunnerTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private PreflightRunner Runner(params IPreflightCheck[] checks)
    {
        return new PreflightRunner(checks, _host.Context());
    }

    private static PreflightState StateOf(PreflightRunner runner, string id)
    {
        return runner.Checks.Single(c => c.Id == id).State;
    }

    [Fact]
    public async Task RunsChecksInDeclaredOrder()
    {
        var ran = new List<string>();
        FakeCheck Make(string id, int order)
        {
            var check = new FakeCheck(id, order);
            check.Body = (_, _, _) =>
            {
                ran.Add(id);
                return Task.FromResult(PreflightResult.Passed("ok"));
            };
            return check;
        }

        // Registered out of order on purpose.
        var runner = Runner(Make("third", 30), Make("first", 10), Make("second", 20));

        Assert.Equal(new[] {"first", "second", "third"}, runner.Checks.Select(c => c.Id));
        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.Equal(new[] {"first", "second", "third"}, ran);
        Assert.True(outcome.Ready);
        Assert.All(outcome.Checks, c => Assert.Equal(PreflightState.Passed, c.State));
    }

    [Fact]
    public void RejectsDependencyThatWouldRunLater()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Runner(new FakeCheck("a", 10, "b"), new FakeCheck("b", 20)));
        Assert.Contains("does not run before", ex.Message);

        Assert.Throws<InvalidOperationException>(() =>
            Runner(new FakeCheck("a", 10, "missing")));
        Assert.Throws<InvalidOperationException>(() =>
            Runner(new FakeCheck("a", 10), new FakeCheck("a", 20)));
    }

    [Theory]
    [InlineData(PreflightState.Failed)]
    [InlineData(PreflightState.NeedsUser)]
    public async Task DependentsAreSkippedWhenADependencyDoesNotPass(PreflightState dependencyState)
    {
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(new PreflightResult(dependencyState, "no"))
        };
        var b = new FakeCheck("b", 20, "a");
        var c = new FakeCheck("c", 30, "b");
        var d = new FakeCheck("d", 40);

        var runner = Runner(a, b, c, d);
        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        Assert.Equal(dependencyState, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Skipped, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Skipped, StateOf(runner, "c"));
        Assert.Equal(PreflightState.Passed, StateOf(runner, "d"));
        Assert.Equal(0, b.Runs);
        Assert.Equal(0, c.Runs);
        Assert.Contains("\"a\"", runner.Checks.Single(x => x.Id == "b").Message);
    }

    [Fact]
    public async Task WarningDependencyStillLetsDependentsRun()
    {
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(PreflightResult.Warning("careful"))
        };
        var b = new FakeCheck("b", 20, "a");

        var runner = Runner(a, b);
        await runner.RunAll(CancellationToken.None);

        Assert.Equal(PreflightState.Warning, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Passed, StateOf(runner, "b"));
    }

    [Fact]
    public async Task RunCheckResetsTransitiveDependentsAndRunAllRevisitsThem()
    {
        var a = new FakeCheck("a", 10);
        var b = new FakeCheck("b", 20, "a");
        var c = new FakeCheck("c", 30, "b");
        var d = new FakeCheck("d", 40);

        var runner = Runner(a, b, c, d);
        await runner.RunAll(CancellationToken.None);

        var result = await runner.RunCheck("a", CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(2, a.Runs);
        Assert.Equal(PreflightState.Pending, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "c"));
        Assert.Equal(PreflightState.Passed, StateOf(runner, "d"));
        Assert.False(runner.Outcome.Ready);

        await runner.RunAll(CancellationToken.None);

        Assert.Equal(2, a.Runs);
        Assert.Equal(2, b.Runs);
        Assert.Equal(2, c.Runs);
        Assert.Equal(1, d.Runs);
        Assert.True(runner.Outcome.Ready);
    }

    [Fact]
    public async Task RunAllSkipsChecksThatAlreadyPassedWithUnchangedDependencies()
    {
        var a = new FakeCheck("a", 10);
        var b = new FakeCheck("b", 20, "a");

        var runner = Runner(a, b);
        await runner.RunAll(CancellationToken.None);
        await runner.RunAll(CancellationToken.None);

        Assert.Equal(1, a.Runs);
        Assert.Equal(1, b.Runs);
    }

    [Fact]
    public async Task FailedChecksRunAgainOnTheNextRunAll()
    {
        var attempts = 0;
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(++attempts == 1
                ? PreflightResult.Failed("first time")
                : PreflightResult.Passed("ok"))
        };
        var b = new FakeCheck("b", 20, "a");

        var runner = Runner(a, b);
        Assert.False((await runner.RunAll(CancellationToken.None)).Ready);
        Assert.True((await runner.RunAll(CancellationToken.None)).Ready);
        Assert.Equal(2, a.Runs);
        Assert.Equal(1, b.Runs);
    }

    [Fact]
    public async Task CancellationMarksTheRunningCheckCancelledAndTheRestPending()
    {
        var started = new TaskCompletionSource();
        var a = new FakeCheck("a", 10);
        var b = new FakeCheck("b", 20)
        {
            Body = async (_, _, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return PreflightResult.Passed("never");
            }
        };
        var c = new FakeCheck("c", 30);

        var runner = Runner(a, b, c);
        var events = new List<PreflightEvent>();
        runner.Changed += e =>
        {
            lock (events)
            {
                events.Add(e);
            }
        };
        using var cts = new CancellationTokenSource();
        var run = runner.RunAll(cts.Token);
        await started.Task;
        cts.Cancel();
        var outcome = await run;

        Assert.False(outcome.Ready);
        Assert.Equal(PreflightState.Passed, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Cancelled, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "c"));
        Assert.Equal(0, c.Runs);
        Assert.Contains(events, e => e is RunFinished);
    }

    [Fact]
    public async Task CancelStopsTheRunInProgress()
    {
        var started = new TaskCompletionSource();
        var a = new FakeCheck("a", 10)
        {
            Body = async (_, _, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return PreflightResult.Passed("never");
            }
        };

        var runner = Runner(a);
        var run = runner.RunAll(CancellationToken.None);
        await started.Task;
        runner.Cancel();
        await run;

        Assert.Equal(PreflightState.Cancelled, StateOf(runner, "a"));
    }

    [Fact]
    public async Task AcknowledgedWarningIsReadyAndUnacknowledgedIsNot()
    {
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(PreflightResult.Warning("low on space",
                actions: new[] {PreflightAction.ContinueAnyway}))
        };

        var runner = Runner(a);
        var outcome = await runner.RunAll(CancellationToken.None);
        Assert.False(outcome.Ready);
        Assert.Equal(PreflightState.Warning, StateOf(runner, "a"));
        Assert.Contains(PreflightAction.ContinueAnyway, runner.Checks.Single().Actions);

        var events = new List<PreflightEvent>();
        runner.Changed += e => events.Add(e);
        runner.Acknowledge("a");

        Assert.True(runner.Outcome.Ready);
        Assert.True(runner.Checks.Single().Acknowledged);
        Assert.Equal(PreflightState.Warning, StateOf(runner, "a"));
        var changed = Assert.IsType<CheckChanged>(Assert.Single(events));
        Assert.True(changed.Status.Acknowledged);

        // Acknowledging a Passed check changes nothing.
        events.Clear();
        runner.Acknowledge("a");
        Assert.Empty(events);
    }

    [Fact]
    public async Task RaisesOneEventPerTransition()
    {
        var a = new FakeCheck("a", 10);
        var runner = Runner(a);
        var events = new List<PreflightEvent>();
        runner.Changed += e =>
        {
            lock (events)
            {
                events.Add(e);
            }
        };
        await runner.RunAll(CancellationToken.None);

        var states = events.OfType<CheckChanged>().Select(e => e.Status.State).ToArray();
        Assert.Equal(new[] {PreflightState.Running, PreflightState.Passed}, states);
        var finished = Assert.IsType<RunFinished>(events.Last());
        Assert.True(finished.Outcome.Ready);
    }

    [Fact]
    public async Task ProgressIsVisibleWhileACheckRuns()
    {
        var reported = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var a = new FakeCheck("a", 10)
        {
            Body = async (_, progress, _) =>
            {
                progress.Report(5, 10, "half way");
                reported.SetResult();
                await release.Task;
                return PreflightResult.Passed("ok");
            }
        };

        var runner = Runner(a);
        var run = runner.RunAll(CancellationToken.None);
        await reported.Task;

        var running = runner.Checks.Single();
        Assert.Equal(PreflightState.Running, running.State);
        Assert.Equal(0.5d, (double) running.Progress, 3);
        Assert.Equal("half way", running.ProgressText);

        release.SetResult();
        await run;

        var passed = runner.Checks.Single();
        Assert.Equal(1d, (double) passed.Progress, 3);
        Assert.Null(passed.ProgressText);
    }

    [Fact]
    public async Task OnlyOneRunAtATime()
    {
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var a = new FakeCheck("a", 10)
        {
            Body = async (_, _, _) =>
            {
                started.TrySetResult();
                await release.Task;
                return PreflightResult.Passed("ok");
            }
        };

        var runner = Runner(a);
        var first = runner.RunAll(CancellationToken.None);
        await started.Task;
        var second = runner.RunAll(CancellationToken.None);
        var third = runner.RunCheck("a", CancellationToken.None);

        await Task.Delay(100);
        Assert.False(second.IsCompleted);
        Assert.False(third.IsCompleted);
        Assert.Equal(1, a.Runs);

        release.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(1, a.MaxConcurrent);
        // The second RunAll found a passed check with nothing changed; RunCheck always re-runs.
        Assert.Equal(2, a.Runs);
    }

    [Fact]
    public async Task ACheckThatThrowsFailsWithRetry()
    {
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => throw new InvalidOperationException("boom")
        };
        var b = new FakeCheck("b", 20, "a");

        var runner = Runner(a, b);
        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        var status = runner.Checks.Single(c => c.Id == "a");
        Assert.Equal(PreflightState.Failed, status.State);
        Assert.Contains("boom", status.Message);
        Assert.Contains(PreflightAction.Retry, status.Actions);
        Assert.Equal(PreflightState.Skipped, StateOf(runner, "b"));
    }

    [Fact]
    public async Task SetGameFolderResetsGameInstalledAndItsDependents()
    {
        var login = new FakeCheck(PreflightCheckIds.NexusLogin, 10);
        var game = new FakeCheck(PreflightCheckIds.GameInstalled, 20);
        var files = new FakeCheck(PreflightCheckIds.GameFiles, 30, PreflightCheckIds.GameInstalled);
        var disk = new FakeCheck(PreflightCheckIds.DiskSpace, 40, PreflightCheckIds.GameFiles);

        var runner = Runner(login, game, files, disk);
        await runner.RunAll(CancellationToken.None);
        Assert.True(runner.Outcome.Ready);

        var folder = _host.Manager.CreateFolder().Path;
        runner.SetGameFolder(folder);

        Assert.Equal(folder, _host.Config.GameFolder);
        Assert.Equal(PreflightState.Passed, StateOf(runner, PreflightCheckIds.NexusLogin));
        Assert.Equal(PreflightState.Pending, StateOf(runner, PreflightCheckIds.GameInstalled));
        Assert.Equal(PreflightState.Pending, StateOf(runner, PreflightCheckIds.GameFiles));
        Assert.Equal(PreflightState.Pending, StateOf(runner, PreflightCheckIds.DiskSpace));
        Assert.False(runner.Outcome.Ready);

        await runner.RunAll(CancellationToken.None);
        Assert.Equal(1, login.Runs);
        Assert.Equal(2, game.Runs);
        Assert.Equal(2, files.Runs);
        Assert.Equal(2, disk.Runs);
    }

    [Fact]
    public async Task ArchivesAreKeyedByNameIgnoringCase()
    {
        var archive = new Archive
        {
            Name = "Foo.7z", Size = 1, State = new Http {Url = new Uri("https://example.invalid/foo")}
        };
        var same = new Archive
        {
            Name = "foo.7z", Size = 1, State = new Http {Url = new Uri("https://example.invalid/foo")}
        };
        var a = new FakeCheck("a", 10)
        {
            Body = (_, progress, _) =>
            {
                progress.Archive(archive, ArchiveState.Missing);
                progress.Archive(same, ArchiveState.Present, "found");
                return Task.FromResult(PreflightResult.Passed("ok"));
            }
        };

        var runner = Runner(a);
        var archiveEvents = new List<ArchiveChanged>();
        runner.Changed += e =>
        {
            if (e is ArchiveChanged ac)
                lock (archiveEvents)
                    archiveEvents.Add(ac);
        };
        await runner.RunAll(CancellationToken.None);

        var status = Assert.Single(runner.Archives).Value;
        Assert.Equal(ArchiveState.Present, status.State);
        Assert.Equal("found", status.Message);
        Assert.True(runner.Archives.ContainsKey("FOO.7Z"));
        Assert.Equal(2, archiveEvents.Count);
    }

    [Fact]
    public async Task RunCheckRejectsUnknownIds()
    {
        var runner = Runner(new FakeCheck("a", 10));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunCheck("nope", CancellationToken.None));
        Assert.Throws<ArgumentException>(() => runner.Acknowledge("nope"));
    }

    [Fact]
    public async Task CancellationKeepsFailedAndNeedsUserResults()
    {
        var started = new TaskCompletionSource();
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(PreflightResult.Failed("version mismatch"))
        };
        var b = new FakeCheck("b", 20)
        {
            Body = (_, _, _) => Task.FromResult(PreflightResult.NeedsUser("log in"))
        };
        var c = new FakeCheck("c", 30, "a");
        var d = new FakeCheck("d", 40)
        {
            Body = async (_, _, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return PreflightResult.Passed("never");
            }
        };
        var e = new FakeCheck("e", 50);

        var runner = Runner(a, b, c, d, e);
        using var cts = new CancellationTokenSource();
        var run = runner.RunAll(cts.Token);
        await started.Task;
        cts.Cancel();
        await run;

        // What the user has to act on survives the cancellation; only what did not get to run is reset.
        Assert.Equal(PreflightState.Failed, StateOf(runner, "a"));
        Assert.Equal("version mismatch", runner.Checks.Single(x => x.Id == "a").Message);
        Assert.Equal(PreflightState.NeedsUser, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "c"));
        Assert.Equal(PreflightState.Cancelled, StateOf(runner, "d"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "e"));
        Assert.Equal(0, e.Runs);
    }

    [Fact]
    public async Task DependentsAreSkippedWhenADependencyIsCancelled()
    {
        var started = new TaskCompletionSource();
        var a = new FakeCheck("a", 10)
        {
            Body = async (_, _, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return PreflightResult.Passed("never");
            }
        };
        var b = new FakeCheck("b", 20, "a");

        var runner = Runner(a, b);
        using var cts = new CancellationTokenSource();
        var run = runner.RunAll(cts.Token);
        await started.Task;
        cts.Cancel();
        await run;

        Assert.Equal(PreflightState.Cancelled, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "b"));

        // Asked for on its own, b is skipped rather than run: its dependency did not pass.
        var result = await runner.RunCheck("b", CancellationToken.None);
        Assert.Equal(PreflightState.Skipped, result.State);
        Assert.Equal(PreflightState.Skipped, StateOf(runner, "b"));
        Assert.Contains("\"a\"", result.Message);
        Assert.Equal(0, b.Runs);
    }

    [Fact]
    public async Task RunCheckOnABlockedCheckReportsSkipped()
    {
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(PreflightResult.Failed("no"))
        };
        var b = new FakeCheck("b", 20, "a");

        var runner = Runner(a, b);
        var events = new List<PreflightEvent>();
        runner.Changed += e =>
        {
            lock (events)
            {
                events.Add(e);
            }
        };

        // a has not even run yet, so b cannot.
        var blocked = await runner.RunCheck("b", CancellationToken.None);
        Assert.Equal(PreflightState.Skipped, blocked.State);
        Assert.Contains("\"a\"", blocked.Message);
        Assert.Equal(PreflightState.Skipped, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "a"));
        Assert.Equal(0, b.Runs);
        Assert.Contains(events, e => e is CheckChanged {Status.State: PreflightState.Skipped});
        Assert.Contains(events, e => e is RunFinished);

        // The same once a has run and failed.
        await runner.RunAll(CancellationToken.None);
        Assert.Equal(PreflightState.Failed, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Skipped, (await runner.RunCheck("b", CancellationToken.None)).State);
        Assert.Equal(0, b.Runs);
    }
}
