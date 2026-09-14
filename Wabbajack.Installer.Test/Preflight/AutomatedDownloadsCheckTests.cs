#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     Runs the check against the fake downloaders registered in <see cref="Startup" />. Every archive gets
///     a URL of its own so the fake server, shared by the whole process, never mixes tests up.
/// </summary>
public class AutomatedDownloadsCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly AutomatedDownloadsCheck _check = new();
    private readonly RecordingProgress _progress = new();
    private readonly string _run = Guid.NewGuid().ToString("N");

    public AutomatedDownloadsCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private Uri Url(string name, string host = "example.invalid")
    {
        return new Uri($"https://{host}/{_run}/{name}");
    }

    private Task<Archive> Http(string name, string content, string host = "example.invalid")
    {
        return PreflightTestHost.ArchiveFor(name, content, new Http {Url = Url(name, host)});
    }

    private static Task<Archive> Nexus(string name, string content)
    {
        return PreflightTestHost.ArchiveFor(name, content,
            new Nexus {Game = Game.SkyrimSpecialEdition, ModID = Random.Shared.Next(), FileID = Random.Shared.Next()});
    }

    private void Serve(Archive archive, string content)
    {
        _host.Server.Serve(archive.State, Encoding.UTF8.GetBytes(content));
    }

    private PreflightContext ContextWithMissing(params Archive[] missing)
    {
        _host.Config.ModList.Archives = missing;
        var ctx = _host.Context();
        ctx.State.RequiredArchives = missing;
        ctx.State.Missing = missing.ToList();
        ctx.State.RemainingDownloadBytes = missing.Sum(a => a.Size);
        return ctx;
    }

    private static void SetPremium(PreflightContext ctx, bool premium)
    {
        ctx.State.Nexus = new NexusLoginStatus(true, true, premium, "someone", null);
    }

    private static ManualQueueItem QueueItem(PreflightContext ctx, string name)
    {
        return ctx.State.ManualQueue.Single(q => q.Archive.Name == name);
    }

    [Fact]
    public async Task DownloadsMissingHttpArchives()
    {
        var one = await Http("one.7z", "first archive");
        var two = await Http("two.7z", "second archive");
        Serve(one, "first archive");
        Serve(two, "second archive");
        var ctx = ContextWithMissing(one, two);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal("2 downloaded", result.Message);
        Assert.Equal("first archive", await _host.Config.Downloads.Combine("one.7z").ReadAllTextAsync());
        Assert.Equal("second archive", await _host.Config.Downloads.Combine("two.7z").ReadAllTextAsync());
        Assert.Equal(_host.Config.Downloads.Combine("one.7z"), ctx.State.HashedArchives["one.7z"]);
        Assert.Equal(_host.Config.Downloads.Combine("two.7z"), ctx.State.HashedArchives["two.7z"]);
        Assert.Empty(ctx.State.Missing);
        Assert.Empty(ctx.State.ManualQueue);
        Assert.Equal(0, ctx.State.RemainingDownloadBytes);

        var states = _progress.LastStates();
        Assert.Equal(ArchiveState.Downloaded, states["one.7z"]);
        Assert.Equal(ArchiveState.Downloaded, states["two.7z"]);
        Assert.Contains(_progress.Archives, a => a.Archive.Name == "one.7z" && a.State == ArchiveState.Downloading);
        Assert.Contains(_progress.Reports, r => r.Current == 2 && r.Total == 2);
    }

    [Fact]
    public async Task WritesMetaAndHashCacheAfterDownload()
    {
        var archive = await Http("meta.7z", "bytes with meta");
        Serve(archive, "bytes with meta");
        var ctx = ContextWithMissing(archive);

        await _check.Run(ctx, _progress, CancellationToken.None);

        var dest = _host.Config.Downloads.Combine("meta.7z");
        var meta = dest.WithExtension(Ext.Meta);
        Assert.True(meta.FileExists());
        var lines = meta.ReadAllLines().ToList();
        Assert.Equal("[General]", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("directURL=") && l.Contains(_run));
        Assert.Equal(archive.Hash, await _host.Cache.TryGetHashCache(dest));
    }

    [Fact]
    public async Task RoutesNonPremiumNexusToManualQueue()
    {
        var nexus = await Nexus("nexus.7z", "nexus bytes");
        Serve(nexus, "nexus bytes");
        var ctx = ContextWithMissing(nexus);
        SetPremium(ctx, false);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.StartsWith("0 downloaded, 1 file still to fetch by hand", result.Message);
        Assert.Contains(PreflightAction.DownloadByHand, result.Actions!);
        Assert.Equal(0, _host.Server.Attempts(nexus.State));
        var item = QueueItem(ctx, "nexus.7z");
        Assert.Contains("nexusmods.com", item.Target.Url.ToString());
        Assert.Contains("premium", item.Reason);
        Assert.Equal(ArchiveState.ManualRequired, _progress.LastStates()["nexus.7z"]);
        Assert.Single(ctx.State.Missing);
        Assert.Equal(nexus.Size, ctx.State.RemainingDownloadBytes);
    }

    [Fact]
    public async Task TreatsPremiumNexusAsAutomated()
    {
        var nexus = await Nexus("premium.7z", "premium bytes");
        Serve(nexus, "premium bytes");
        var ctx = ContextWithMissing(nexus);
        SetPremium(ctx, true);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(1, _host.Server.Attempts(nexus.State));
        Assert.Empty(ctx.State.ManualQueue);
        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(ArchiveState.Downloaded, _progress.LastStates()["premium.7z"]);
    }

    [Fact]
    public async Task RoutesMediaFireAndFriendsToManualByStateType()
    {
        var archives = new[]
        {
            await PreflightTestHost.ArchiveFor("mediafire.7z", "a", new MediaFire {Url = Url("mediafire.7z", "www.mediafire.com")}),
            await PreflightTestHost.ArchiveFor("mega.7z", "b", new Mega {Url = Url("mega.7z", "mega.nz")}),
            await PreflightTestHost.ArchiveFor("gdrive.7z", "c", new GoogleDrive {Id = "abc123"}),
            await PreflightTestHost.ArchiveFor("moddb.7z", "d", new ModDB {Url = Url("moddb.7z", "www.moddb.com")}),
            await PreflightTestHost.ArchiveFor("manual.7z", "e", new Manual {Url = Url("manual.7z"), Prompt = "Click the big button"}),
            await PreflightTestHost.ArchiveFor("ll.7z", "f", new LoversLab {IPS4Mod = 42, IPS4File = "ll.7z"}),
            await PreflightTestHost.ArchiveFor("vp.7z", "g", new VectorPlexus {IPS4Mod = 43, IPS4File = "vp.7z"})
        };
        var ctx = ContextWithMissing(archives);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.StartsWith("0 downloaded, 7 files still to fetch by hand", result.Message);
        Assert.Equal(archives.Select(a => a.Name), ctx.State.ManualQueue.Select(q => q.Archive.Name));
        Assert.DoesNotContain(_host.Server.AllAttempts, k => k.Contains(_run));
        Assert.All(archives, a => Assert.Equal(ArchiveState.ManualRequired, _progress.LastStates()[a.Name]));
        Assert.Equal("Click the big button", QueueItem(ctx, "manual.7z").Target.Instructions);
        Assert.Contains("drive.google.com", QueueItem(ctx, "gdrive.7z").Target.Url.ToString());
        Assert.Equal(archives.Sum(a => a.Size), ctx.State.RemainingDownloadBytes);
    }

    [Fact]
    public async Task HashMismatchDeletesFileAndRoutesToManual()
    {
        var archive = await Http("corrupt.7z", "the right bytes");
        Serve(archive, "not the right bytes");
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.Equal(ArchiveDownloadPipeline.HashMismatchAttempts, _host.Server.Attempts(archive.State));
        Assert.False(_host.Config.Downloads.Combine("corrupt.7z").FileExists());
        Assert.Contains("did not match", QueueItem(ctx, "corrupt.7z").Reason);
        Assert.Equal(ArchiveState.ManualRequired, _progress.LastStates()["corrupt.7z"]);
        Assert.False(ctx.State.HashedArchives.ContainsKey("corrupt.7z"));
    }

    [Fact]
    public async Task TimeoutIsRetriedThenRoutedToManual()
    {
        var archive = await Http("slow.7z", "slow bytes");
        _host.Server.FailNext(archive.State, ArchiveDownloadPipeline.TransientAttempts,
            () => new TaskCanceledException("stalled"));
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.Equal(ArchiveDownloadPipeline.TransientAttempts, _host.Server.Attempts(archive.State));
        Assert.Contains("stalled", QueueItem(ctx, "slow.7z").Reason);
        Assert.Equal(ArchiveState.ManualRequired, _progress.LastStates()["slow.7z"]);
    }

    [Fact]
    public async Task HttpErrorIsRetriedThenRoutedToManual()
    {
        var archive = await Http("refused.7z", "refused bytes");
        _host.Server.FailNext(archive.State, ArchiveDownloadPipeline.TransientAttempts,
            () => new Networking.Http.HttpException(503, "Service Unavailable"));
        var ctx = ContextWithMissing(archive);

        await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(ArchiveDownloadPipeline.TransientAttempts, _host.Server.Attempts(archive.State));
        Assert.Contains("503", QueueItem(ctx, "refused.7z").Reason);
    }

    [Fact]
    public async Task TransientFailureFollowedBySuccessDownloads()
    {
        var archive = await Http("flaky.7z", "flaky bytes");
        Serve(archive, "flaky bytes");
        _host.Server.FailNext(archive.State, 1, () => new TaskCanceledException("stalled once"));
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal("1 downloaded", result.Message);
        Assert.Equal(2, _host.Server.Attempts(archive.State));
        Assert.Empty(ctx.State.ManualQueue);
    }

    [Fact]
    public async Task AllowListRejectionRoutesToManualWithoutAborting()
    {
        var allowed = await Http("allowed.7z", "allowed bytes", "allowed.invalid");
        var blocked = await Http("blocked.7z", "blocked bytes", "blocked.invalid");
        Serve(allowed, "allowed bytes");
        Serve(blocked, "blocked bytes");
        _host.Policy.AllowListValue = new ServerAllowList
        {
            AllowedPrefixes = new[] {"https://allowed.invalid/"},
            GoogleIDs = Array.Empty<string>()
        };
        var ctx = ContextWithMissing(blocked, allowed);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.StartsWith("1 downloaded, 1 file still to fetch by hand", result.Message);
        Assert.Equal(1, _host.Server.Attempts(allowed.State));
        Assert.Equal(0, _host.Server.Attempts(blocked.State));
        Assert.Contains("allow-list", QueueItem(ctx, "blocked.7z").Reason);
        Assert.True(ctx.State.HashedArchives.ContainsKey("allowed.7z"));
    }

    [Fact]
    public async Task MirrorReroutesState()
    {
        var archive = await Http("mirrored.7z", "mirrored bytes", "dead.invalid");
        var original = archive.State;
        var mirror = new Http {Url = Url("mirrored.7z", "mirror.invalid")};
        _host.Server.Serve(mirror, Encoding.UTF8.GetBytes("mirrored bytes"));
        _host.Policy.MirrorArchives.Add(new Archive
        {
            Name = archive.Name, Hash = archive.Hash, Size = archive.Size, State = mirror
        });
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal("1 downloaded", result.Message);
        Assert.Same(mirror, archive.State);
        Assert.Equal(0, _host.Server.Attempts(original));
        Assert.Equal(1, _host.Server.Attempts(mirror));
        var meta = _host.Config.Downloads.Combine("mirrored.7z").WithExtension(Ext.Meta).ReadAllLines();
        Assert.Contains(meta, l => l.Contains("mirror.invalid"));
    }

    /// <summary>Points the mirror list at a Nexus copy of <paramref name="archive" /> and serves those bytes.</summary>
    private Nexus MirrorOnNexus(Archive archive, string content)
    {
        var mirror = new Nexus {Game = Game.SkyrimSpecialEdition, ModID = Random.Shared.Next(), FileID = Random.Shared.Next()};
        _host.Server.Serve(mirror, Encoding.UTF8.GetBytes(content));
        _host.Policy.MirrorArchives.Add(new Archive
        {
            Name = archive.Name, Hash = archive.Hash, Size = archive.Size, State = mirror
        });
        return mirror;
    }

    [Fact]
    public async Task MirrorRerouteToNexusProbesTheAccountOnceAndDownloadsForPremium()
    {
        // The list has no Nexus archives, so nexus-login recorded nothing; the reroute happens after it ran.
        var archive = await Http("rerouted.7z", "rerouted bytes", "dead.invalid");
        var mirror = MirrorOnNexus(archive, "rerouted bytes");
        _host.Nexus.Status = new NexusLoginStatus(true, true, true, "someone", null);
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal("1 downloaded", result.Message);
        Assert.Equal(1, _host.Nexus.Calls);
        Assert.Equal(1, _host.Server.Attempts(mirror));
        Assert.Empty(ctx.State.ManualQueue);
        Assert.True(ctx.State.Nexus!.IsPremium);
        Assert.Equal(ArchiveState.Downloaded, _progress.LastStates()["rerouted.7z"]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task MirrorRerouteToNexusWithoutPremiumGoesManualWithTheNexusPage(bool hasToken, bool loggedIn)
    {
        var archive = await Http("rerouted.7z", "rerouted bytes", "dead.invalid");
        var mirror = MirrorOnNexus(archive, "rerouted bytes");
        _host.Nexus.Status = new NexusLoginStatus(hasToken, loggedIn, false, loggedIn ? "someone" : null, null);
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.StartsWith("0 downloaded, 1 file still to fetch by hand", result.Message);
        Assert.Equal(1, _host.Nexus.Calls);
        Assert.Equal(0, _host.Server.Attempts(mirror));
        var item = QueueItem(ctx, "rerouted.7z");
        // A rerouted archive carries the mirror's Nexus state, which is built here rather than read from
        // the list, so it is worth pinning that it still reaches the browser as a file link and not as the
        // mod page.
        Assert.Equal(
            $"https://www.nexusmods.com/skyrimspecialedition/mods/{mirror.ModID}?tab=files&file_id={mirror.FileID}",
            item.Target.Url.AbsoluteUri);
        Assert.Contains("premium", item.Reason);
        Assert.Equal(ArchiveState.ManualRequired, _progress.LastStates()["rerouted.7z"]);
    }

    [Fact]
    public async Task MirrorRerouteToNexusReusesWhatNexusLoginRecorded()
    {
        var archive = await Http("rerouted.7z", "rerouted bytes", "dead.invalid");
        var mirror = MirrorOnNexus(archive, "rerouted bytes");
        _host.Nexus.Status = new NexusLoginStatus(true, true, true, "someone", null);
        var ctx = ContextWithMissing(archive);
        SetPremium(ctx, true);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal("1 downloaded", result.Message);
        Assert.Equal(0, _host.Nexus.Calls);
        Assert.Equal(1, _host.Server.Attempts(mirror));
    }

    [Fact]
    public async Task MirrorRerouteWithoutNexusDoesNotProbe()
    {
        var archive = await Http("mirrored.7z", "mirrored bytes", "dead.invalid");
        var mirror = new Http {Url = Url("mirrored.7z", "mirror.invalid")};
        _host.Server.Serve(mirror, Encoding.UTF8.GetBytes("mirrored bytes"));
        _host.Policy.MirrorArchives.Add(new Archive
        {
            Name = archive.Name, Hash = archive.Hash, Size = archive.Size, State = mirror
        });
        var ctx = ContextWithMissing(archive);

        await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(0, _host.Nexus.Calls);
        Assert.Null(ctx.State.Nexus);
    }

    [Fact]
    public async Task PolicyLoadFailureFailsCheckWithRetry()
    {
        var archive = await Http("never.7z", "never fetched");
        Serve(archive, "never fetched");
        _host.Policy.Throw = new HttpRequestException("no network");
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("download rules", result.Message);
        Assert.Contains("no network", result.Message);
        Assert.Contains(PreflightAction.Retry, result.Actions!);
        Assert.Equal(0, _host.Server.Attempts(archive.State));
        Assert.Single(ctx.State.Missing);
    }

    [Fact]
    public async Task ManualDownloadRequiredExceptionRoutesToManual()
    {
        var archive = await Http("browser.7z", "browser bytes");
        var target = new ManualDownloadTarget(new Uri("https://page.invalid/browser"), "Some Site", "Press download");
        _host.Server.FailNext(archive.State, 1,
            () => new ManualDownloadRequiredException(archive, target, "needs a browser"));
        var ctx = ContextWithMissing(archive);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.Contains(PreflightAction.DownloadByHand, result.Actions!);
        var item = QueueItem(ctx, "browser.7z");
        Assert.Same(target, item.Target);
        Assert.Equal("needs a browser", item.Reason);
        Assert.Equal(ArchiveState.ManualRequired, _progress.LastStates()["browser.7z"]);
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        var archive = await Http("cancelled.7z", "cancelled bytes");
        _host.Server.FailNext(archive.State, 1, () =>
        {
            cts.Cancel();
            return new OperationCanceledException(cts.Token);
        });
        var ctx = ContextWithMissing(archive);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _check.Run(ctx, _progress, cts.Token));

        Assert.Equal(1, _host.Server.Attempts(archive.State));
        Assert.Empty(ctx.State.ManualQueue);
        Assert.DoesNotContain(_progress.Archives, a => a.State == ArchiveState.ManualRequired);
    }

    [Fact]
    public async Task NothingMissingPasses()
    {
        var ctx = ContextWithMissing();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);
        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(0, ctx.State.RemainingDownloadBytes);
    }
}
