#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     The two download checks driven by the runner, in the order it runs them: manual-downloads first, so
///     the user is finished with the part that needs their hands before the automated pass they can walk
///     away from starts. The checks upstream of them are faked; the state they would have left is set here.
/// </summary>
public class DownloadOrderTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly string _run = Guid.NewGuid().ToString("N");
    private readonly AbsolutePath _watch;

    /// <summary>Ids of the checks that started running, in the order they did.</summary>
    private readonly List<string> _ran = new();

    public DownloadOrderTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
        _watch = _host.Manager.CreateFolder().Path;
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private Uri Url(string name, string host = "example.invalid")
    {
        return new Uri($"https://{host}/{_run}/{name}");
    }

    private async Task<Archive> Http(string name, string content, string host = "example.invalid")
    {
        var archive = await PreflightTestHost.ArchiveFor(name, content, new Http {Url = Url(name, host)});
        _host.Server.Serve(archive.State, Encoding.UTF8.GetBytes(content));
        return archive;
    }

    private Task<Archive> ByHand(string name, string content)
    {
        return PreflightTestHost.ArchiveFor(name, content,
            new Manual {Url = Url(name), Prompt = "Press download"});
    }

    private PreflightContext Context(IManualDownloadAcquirer acquirer, params Archive[] missing)
    {
        _host.Config.ModList.Archives = missing;
        var options = new PreflightOptions
        {
            SendMetrics = false, DownloadRetryDelay = TimeSpan.FromMilliseconds(20), WatchFolder = _watch
        };
        var ctx = _host.Context(options, acquirer);
        ctx.State.RequiredArchives = missing;
        ctx.State.Missing = missing.ToList();
        ctx.State.RemainingDownloadBytes = missing.Sum(a => a.Size);
        return ctx;
    }

    /// <summary>The real download checks, with the three they depend on faked out.</summary>
    private PreflightRunner Runner(PreflightContext ctx)
    {
        var runner = new PreflightRunner(new IPreflightCheck[]
        {
            new FakeCheck(PreflightCheckIds.NexusLogin, 100),
            new FakeCheck(PreflightCheckIds.ArchiveInventory, 400),
            new FakeCheck(PreflightCheckIds.UnsupportedArchives, 500),
            new ManualDownloadsCheck(),
            new AutomatedDownloadsCheck()
        }, ctx);

        runner.Changed += evt =>
        {
            if (evt is not CheckChanged changed || changed.Status.State != PreflightState.Running) return;
            lock (_ran)
            {
                // Progress reports come through as further Running events for the same check.
                if (_ran.LastOrDefault() != changed.Status.Id) _ran.Add(changed.Status.Id);
            }
        };
        return runner;
    }

    private static CheckStatus StatusOf(PreflightRunner runner, string id)
    {
        return runner.Checks.Single(c => c.Id == id);
    }

    private IReadOnlyList<string> DownloadChecksInRunOrder()
    {
        lock (_ran)
        {
            return _ran.Where(id => id is PreflightCheckIds.ManualDownloads or PreflightCheckIds.AutomatedDownloads)
                .ToList();
        }
    }

    [Fact]
    public async Task ManualDownloadsRunsBeforeAutomatedDownloads()
    {
        var automated = await Http("auto.7z", "downloaded for you");
        var byHand = await ByHand("byhand.7z", "fetched by hand");
        var acquirer = new FakeManualDownloadAcquirer();
        var ctx = Context(acquirer, automated, byHand);
        var runner = Runner(ctx);

        // The user's part is done while the automated pass has not started: the file is in place by the
        // time the acquirer is asked about it.
        var placed = await PreflightTestHost.WriteFile(_host.Config.Downloads.Combine("byhand.7z"), "fetched by hand");
        acquirer.Results["byhand.7z"] = (ManualDownloadState.Moved, placed);

        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.True(outcome.Ready);
        Assert.Equal(new[] {PreflightCheckIds.ManualDownloads, PreflightCheckIds.AutomatedDownloads},
            DownloadChecksInRunOrder());
        Assert.Equal(new[] {"byhand.7z"}, acquirer.Started.Select(a => a.Name));
        Assert.Equal(1, _host.Server.Attempts(automated.State));
        Assert.Equal("1 file downloaded by hand and verified",
            StatusOf(runner, PreflightCheckIds.ManualDownloads).Message);
        Assert.Equal("1 downloaded", StatusOf(runner, PreflightCheckIds.AutomatedDownloads).Message);
        Assert.Empty(ctx.State.Missing);
    }

    [Fact]
    public async Task NothingToFetchByHandPassesStraightThrough()
    {
        var one = await Http("one.7z", "first archive");
        var two = await Http("two.7z", "second archive");
        var acquirer = new FakeManualDownloadAcquirer();
        var ctx = Context(acquirer, one, two);
        var runner = Runner(ctx);

        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.True(outcome.Ready);
        Assert.Equal("Nothing to download by hand", StatusOf(runner, PreflightCheckIds.ManualDownloads).Message);
        Assert.Equal("2 downloaded", StatusOf(runner, PreflightCheckIds.AutomatedDownloads).Message);
        Assert.Empty(acquirer.Started);
        Assert.Empty(ctx.State.ManualQueue);
        Assert.Equal(0, ctx.State.RemainingDownloadBytes);
    }

    [Fact]
    public async Task PartitionIsComputedOnceAndReusedByBothChecks()
    {
        // A mirror pointing at Nexus makes every part of the plan do some work: the allow-list, the mirror
        // list and the account probe nexus-login never made.
        var archive = await Http("rerouted.7z", "rerouted bytes", "dead.invalid");
        var mirror = new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1234, FileID = 5678};
        _host.Server.Serve(mirror, Encoding.UTF8.GetBytes("rerouted bytes"));
        _host.Policy.MirrorArchives.Add(new Archive
        {
            Name = archive.Name, Hash = archive.Hash, Size = archive.Size, State = mirror
        });
        _host.Nexus.Status = new NexusLoginStatus(true, true, true, "someone", null);
        var ctx = Context(new FakeManualDownloadAcquirer(), archive);
        var runner = Runner(ctx);

        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.True(outcome.Ready);
        Assert.Equal(1, _host.Policy.AllowListCalls);
        Assert.Equal(1, _host.Policy.MirrorCalls);
        Assert.Equal(1, _host.Nexus.Calls);
        Assert.Equal(1, _host.Server.Attempts(mirror));
        Assert.True(ctx.State.Nexus!.IsPremium);
    }

    /// <summary>
    ///     The case the order creates: an automated download that turns out to need a browser lands in the
    ///     queue after manual-downloads has already passed. The run must not be called ready until someone
    ///     has worked those, which is what the check's action is for.
    /// </summary>
    [Fact]
    public async Task LateManualRequirementNeedsUserAndIsClearedByRerunningManualDownloads()
    {
        var archive = await Http("browser.7z", "browser bytes");
        var target = new ManualDownloadTarget(new Uri("https://page.invalid/browser"), "Some Site", "Press download");
        _host.Server.FailNext(archive.State, 1,
            () => new ManualDownloadRequiredException(archive, target, "needs a browser"));
        var acquirer = new FakeManualDownloadAcquirer();
        var ctx = Context(acquirer, archive);
        var runner = Runner(ctx);

        var first = await runner.RunAll(CancellationToken.None);

        Assert.False(first.Ready);
        Assert.Equal(PreflightState.Passed, StatusOf(runner, PreflightCheckIds.ManualDownloads).State);
        var automated = StatusOf(runner, PreflightCheckIds.AutomatedDownloads);
        Assert.Equal(PreflightState.NeedsUser, automated.State);
        Assert.Equal(PreflightAction.DownloadByHand, Assert.Single(automated.Actions));
        Assert.Contains("https://page.invalid/browser", automated.Detail);
        Assert.Equal(new[] {"browser.7z"}, ctx.State.ManualQueue.Select(q => q.Archive.Name));
        Assert.Same(target, ctx.State.ManualQueue.Single().Target);
        Assert.Equal(ArchiveState.ManualRequired, runner.Archives["browser.7z"].State);

        // The user does what the action offers: the file lands and manual-downloads is run again.
        var placed = await PreflightTestHost.WriteFile(_host.Config.Downloads.Combine("browser.7z"), "browser bytes");
        acquirer.Results["browser.7z"] = (ManualDownloadState.Moved, placed);

        var rerun = await runner.RunCheck(PreflightCheckIds.ManualDownloads, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, rerun.State);
        Assert.Equal("1 file downloaded by hand and verified", rerun.Message);
        Assert.Equal(new[] {"browser.7z"}, acquirer.Started.Select(a => a.Name));
        Assert.Equal(placed, ctx.State.HashedArchives["browser.7z"]);
        Assert.Empty(ctx.State.ManualQueue);

        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.True(outcome.Ready);
        Assert.Equal(PreflightState.Passed, StatusOf(runner, PreflightCheckIds.AutomatedDownloads).State);
        Assert.Equal(1, _host.Server.Attempts(archive.State));
        Assert.Empty(ctx.State.Missing);
        Assert.Equal(0, ctx.State.RemainingDownloadBytes);
    }

    [Fact]
    public async Task RetryingAutomatedDownloadsLeavesAQueuedArchiveAlone()
    {
        var archive = await Http("browser.7z", "browser bytes");
        var target = new ManualDownloadTarget(new Uri("https://page.invalid/browser"), "Some Site", "Press download");
        _host.Server.FailNext(archive.State, 1,
            () => new ManualDownloadRequiredException(archive, target, "needs a browser"));
        var ctx = Context(new FakeManualDownloadAcquirer(), archive);
        var runner = Runner(ctx);

        await runner.RunAll(CancellationToken.None);
        var again = await runner.RunCheck(PreflightCheckIds.AutomatedDownloads, CancellationToken.None);

        // Downloading it a second time would only send it to the queue a second time, so it is left there.
        Assert.Equal(PreflightState.NeedsUser, again.State);
        Assert.StartsWith("0 downloaded, 1 still to fetch by hand", again.Message);
        Assert.Equal(1, _host.Server.Attempts(archive.State));
        Assert.Equal(new[] {"browser.7z"}, ctx.State.ManualQueue.Select(q => q.Archive.Name));
    }
}
