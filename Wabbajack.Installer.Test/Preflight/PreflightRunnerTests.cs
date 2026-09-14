#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class PreflightRunnerTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly IServiceProvider _provider;

    public PreflightRunnerTests(IServiceProvider provider)
    {
        _provider = provider;
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
        // The run stops at "a", so the dependents are only reachable through RunCheck. What matters here is
        // the reason they refuse to run, which is the same one either way.
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

        Assert.Equal(PreflightState.Skipped, (await runner.RunCheck("b", CancellationToken.None)).State);
        Assert.Equal(PreflightState.Skipped, (await runner.RunCheck("c", CancellationToken.None)).State);
        Assert.Equal(0, b.Runs);
        Assert.Equal(0, c.Runs);
        Assert.Equal(0, d.Runs);
        Assert.Contains("\"a\"", runner.Checks.Single(x => x.Id == "b").Message);
    }

    /// <summary>
    ///     The halt rule, and the reason for it: with a failed login the user should be told to log in, not
    ///     handed the consequences of not having done so. Independent checks later in the order are exactly
    ///     what used to keep going - the download checks are independent of nothing, but game files and disk
    ///     space are - so an independent check is what this pins.
    /// </summary>
    [Theory]
    [InlineData(PreflightState.Failed)]
    [InlineData(PreflightState.NeedsUser)]
    public async Task AFailedCheckStopsTheRunAndLaterIndependentChecksDoNotRun(PreflightState state)
    {
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(new PreflightResult(state, "no"))
        };
        var b = new FakeCheck("b", 20);
        var c = new FakeCheck("c", 30);

        var runner = Runner(a, b, c);
        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        Assert.Equal(state, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "c"));
        Assert.Equal(0, b.Runs);
        Assert.Equal(0, c.Runs);
    }

    /// <summary>
    ///     manual-downloads and automated-downloads end NeedsUser in normal flow: the user has files to fetch,
    ///     which is work rather than a mistake, and disk-space still has something worth saying.
    /// </summary>
    [Fact]
    public async Task ANeedsUserTheCheckCallsNormalDoesNotStopTheRun()
    {
        var a = new FakeCheck("a", 10)
        {
            NeedsUserStopsRun = false,
            Body = (_, _, _) => Task.FromResult(PreflightResult.NeedsUser("fetch these"))
        };
        var b = new FakeCheck("b", 20);

        var runner = Runner(a, b);
        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        Assert.Equal(PreflightState.NeedsUser, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Passed, StateOf(runner, "b"));
        Assert.Equal(1, b.Runs);
    }

    /// <summary>
    ///     A dependent of a check that ended NeedsUser without stopping the run is still Skipped, with the
    ///     reason naming what it was waiting for; an unrelated check further down still runs.
    /// </summary>
    [Fact]
    public async Task ANonStoppingNeedsUserStillSkipsItsDependents()
    {
        var a = new FakeCheck("a", 10)
        {
            NeedsUserStopsRun = false,
            Body = (_, _, _) => Task.FromResult(PreflightResult.NeedsUser("fetch these"))
        };
        var b = new FakeCheck("b", 20, "a");
        var c = new FakeCheck("c", 30);

        var runner = Runner(a, b, c);
        await runner.RunAll(CancellationToken.None);

        Assert.Equal(PreflightState.Skipped, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Passed, StateOf(runner, "c"));
        Assert.Equal(0, b.Runs);
        Assert.Contains("\"a\"", runner.Checks.Single(x => x.Id == "b").Message);
    }

    /// <summary>
    ///     A Warning never stops a run, acknowledged or not: it is a judgement the user makes, and they should
    ///     be making it with the rest of the checklist in front of them.
    /// </summary>
    [Fact]
    public async Task AWarningDoesNotStopTheRunAndAcknowledgingItMakesTheRunReady()
    {
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(PreflightResult.Warning("careful"))
        };
        var b = new FakeCheck("b", 20);

        var runner = Runner(a, b);
        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        Assert.Equal(PreflightState.Warning, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Passed, StateOf(runner, "b"));
        Assert.Equal(1, b.Runs);

        runner.Acknowledge("a");
        Assert.True(runner.Outcome.Ready);
    }

    /// <summary>
    ///     The reported bug end to end, over the checks the app actually registers: a Nexus list, no login the
    ///     downloader can use, and NEXUS_API_KEY sitting in the environment. The user gets one thing to do -
    ///     log in - and not a queue of Nexus links, because the checks that would have built that queue never
    ///     run.
    /// </summary>
    [Fact]
    public async Task ANexusListWithoutALoginStopsAtTheLoginAndQueuesNothing()
    {
        _host.Config.ModList.Archives = new[]
        {
            await PreflightTestHost.ArchiveFor("one.7z", "nexus one",
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 1}),
            await PreflightTestHost.ArchiveFor("two.7z", "nexus two!",
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 2, FileID = 2})
        };
        _host.Nexus.Status = new NexusLoginStatus(false, false, null, null, NexusCredentialSource.EnvironmentApiKey);

        var context = _host.Context();
        var runner = new PreflightRunner(_provider.GetServices<IPreflightCheck>(), context);
        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        var login = outcome.Checks.Single(c => c.Id == PreflightCheckIds.NexusLogin);
        Assert.Equal(PreflightState.NeedsUser, login.State);
        Assert.Contains(PreflightAction.Login, login.Actions);
        Assert.Contains("NEXUS_API_KEY", login.Detail!);

        foreach (var id in new[]
                 {
                     PreflightCheckIds.GameInstalled, PreflightCheckIds.GameFiles, PreflightCheckIds.ArchiveInventory,
                     PreflightCheckIds.UnsupportedArchives, PreflightCheckIds.ManualDownloads,
                     PreflightCheckIds.AutomatedDownloads, PreflightCheckIds.DiskSpace
                 })
            Assert.Equal(PreflightState.Pending, StateOf(runner, id));

        Assert.Empty(context.State.ManualQueue);
        Assert.Empty(runner.Archives);
    }

    /// <summary>
    ///     What a stopped run leaves behind has to be resumable, because that is the whole user-facing point:
    ///     fix the one thing, and the checklist carries on from where it stopped rather than starting over.
    /// </summary>
    [Fact]
    public async Task FixingTheStoppingCheckLetsTheRunCarryOn()
    {
        var attempts = 0;
        var a = new FakeCheck("a", 10)
        {
            Body = (_, _, _) => Task.FromResult(++attempts == 1
                ? PreflightResult.NeedsUser("log in")
                : PreflightResult.Passed("ok"))
        };
        var b = new FakeCheck("b", 20);
        var c = new FakeCheck("c", 30);

        var runner = Runner(a, b, c);
        Assert.False((await runner.RunAll(CancellationToken.None)).Ready);
        Assert.Equal(0, b.Runs);

        Assert.Equal(PreflightState.Passed, (await runner.RunCheck("a", CancellationToken.None)).State);
        Assert.True((await runner.RunAll(CancellationToken.None)).Ready);
        Assert.Equal(2, a.Runs);
        Assert.Equal(1, b.Runs);
        Assert.Equal(1, c.Runs);
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
        // A throw is a failure like any other, so the run stops there and "b" never gets its turn.
        Assert.Equal(PreflightState.Pending, StateOf(runner, "b"));
        Assert.Equal(0, b.Runs);
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
        // A NeedsUser the check calls normal, so the run reaches the one that hangs; a stopping one would
        // end the run itself and there would be nothing left to cancel.
        var a = new FakeCheck("a", 10)
        {
            NeedsUserStopsRun = false,
            Body = (_, _, _) => Task.FromResult(PreflightResult.NeedsUser("log in"))
        };
        var b = new FakeCheck("b", 20, "a");
        var c = new FakeCheck("c", 30)
        {
            Body = async (_, _, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return PreflightResult.Passed("never");
            }
        };
        var d = new FakeCheck("d", 40);
        // Failed on its own, the way a retried check leaves a result the run never reaches.
        var e = new FakeCheck("e", 50)
        {
            Body = (_, _, _) => Task.FromResult(PreflightResult.Failed("version mismatch"))
        };

        var runner = Runner(a, b, c, d, e);
        Assert.Equal(PreflightState.Failed, (await runner.RunCheck("e", CancellationToken.None)).State);

        using var cts = new CancellationTokenSource();
        var run = runner.RunAll(cts.Token);
        await started.Task;
        cts.Cancel();
        await run;

        // What the user has to act on survives the cancellation; only what did not get to run is reset.
        Assert.Equal(PreflightState.NeedsUser, StateOf(runner, "a"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "b"));
        Assert.Equal(PreflightState.Cancelled, StateOf(runner, "c"));
        Assert.Equal(PreflightState.Pending, StateOf(runner, "d"));
        Assert.Equal(PreflightState.Failed, StateOf(runner, "e"));
        Assert.Equal("version mismatch", runner.Checks.Single(x => x.Id == "e").Message);
        Assert.Equal(0, d.Runs);
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

    /// <summary>
    ///     The WPF app starts a run from the UI thread. If the run kept that thread's synchronization
    ///     context, every await in every check - the hashing loops included - would resume on it and the
    ///     window would stop redrawing. The engine has to leave the caller's context on its own rather than
    ///     leaving it to hosts or to a ConfigureAwait on every call site.
    /// </summary>
    [Fact]
    public async Task ARunDoesNotKeepTheCallersSynchronizationContext()
    {
        const int awaitsPerCheck = 200;

        SynchronizationContext? seenByRunAll = null;
        SynchronizationContext? seenByRunCheck = null;

        var check = new FakeCheck("only", 10);
        var runner = Runner(check);

        using var caller = new PumpedSynchronizationContext();
        await caller.Run(async () =>
        {
            check.Body = async (_, _, _) =>
            {
                seenByRunAll = SynchronizationContext.Current;
                for (var i = 0; i < awaitsPerCheck; i++) await Task.Yield();
                return PreflightResult.Passed("ok");
            };
            await runner.RunAll(CancellationToken.None);

            // The caller gets its own thread back, the way a UI thread does, so the second call is made
            // under the context just as the first was rather than under whatever the first await left.
            Assert.Same(caller, SynchronizationContext.Current);

            check.Body = async (_, _, _) =>
            {
                seenByRunCheck = SynchronizationContext.Current;
                for (var i = 0; i < awaitsPerCheck; i++) await Task.Yield();
                return PreflightResult.Passed("ok");
            };
            await runner.RunCheck("only", CancellationToken.None);
        });

        Assert.NotSame(caller, seenByRunAll);
        Assert.NotSame(caller, seenByRunCheck);

        // The two calls above hand their own continuations back, which is the caller's business. The four
        // hundred awaits inside the checks must not land there too, and every one of them used to.
        Assert.True(caller.Posted < awaitsPerCheck,
            $"the caller's context was asked to run {caller.Posted} continuations");
    }

    /// <summary>
    ///     A check hashing a downloads folder reports progress per file, which is tens of thousands of calls.
    ///     Every one of them reaching the host would be one dispatcher operation each; the runner collapses
    ///     them to roughly one per throttle window.
    /// </summary>
    [Fact]
    public async Task AStormOfProgressReportsIsCollapsed()
    {
        const int reports = 50000;

        var check = new FakeCheck("busy", 10)
        {
            Body = (_, progress, _) =>
            {
                for (var i = 0; i < reports; i++) progress.Report(i, reports);
                return Task.FromResult(PreflightResult.Passed("ok"));
            }
        };

        var runner = Runner(check);
        var changes = 0;
        runner.Changed += e =>
        {
            if (e is CheckChanged) Interlocked.Increment(ref changes);
        };

        await runner.RunAll(CancellationToken.None);

        // Running, then at most one tick per throttle window, then the result. The loop is far quicker than
        // a single window, so this is a handful either way; what it pins is that it is not per report.
        Assert.InRange(changes, 2, 100);
        Assert.Equal(Percent.One, runner.Checks.Single().Progress);
    }

    /// <summary>
    ///     Stands in for a UI thread: one thread with its own queue, and the context stays current on it, so
    ///     a continuation posted back runs with the context still in place and whatever that continuation
    ///     goes on to await comes back here as well.
    ///     <para>
    ///         A plain <see cref="SynchronizationContext" /> cannot show this. Its Post hands the callback to
    ///         the thread pool, where the context is gone, so a chain of a hundred awaits posts once rather
    ///         than a hundred times and the count says nothing about what a dispatcher would have been asked
    ///         to run.
    ///     </para>
    /// </summary>
    private sealed class PumpedSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        private readonly Thread _thread;
        private int _posted;

        public PumpedSynchronizationContext()
        {
            _thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var work in _queue.GetConsumingEnumerable()) work.Callback(work.State);
            }) {IsBackground = true, Name = "preflight test pump"};
            _thread.Start();
        }

        /// <summary>How many continuations this context has been asked to run.</summary>
        public int Posted => Volatile.Read(ref _posted);

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join(TimeSpan.FromSeconds(5));
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posted);
            _queue.Add((d, state));
        }

        /// <summary>Runs <paramref name="body" /> on the pump thread; the returned task completes with it.</summary>
        public Task Run(Func<Task> body)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            SendOrPostCallback start = async _ =>
            {
                try
                {
                    await body();
                    done.SetResult();
                }
                catch (Exception ex)
                {
                    done.SetException(ex);
                }
            };

            // Straight onto the queue rather than through Post: starting the body is the test's doing, and
            // it is the code under test that Posted is counting.
            _queue.Add((start, null));
            return done.Task;
        }
    }
}
