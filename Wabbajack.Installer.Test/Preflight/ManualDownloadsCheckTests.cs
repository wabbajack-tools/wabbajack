#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class ManualDownloadsCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly ManualDownloadsCheck _check = new();
    private readonly RecordingProgress _progress = new();
    private readonly AbsolutePath _watch;

    public ManualDownloadsCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
        _watch = _host.Manager.CreateFolder().Path;
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static async Task<ManualQueueItem> Queued(string name, string content)
    {
        var url = new Uri($"https://example.invalid/{name}");
        var archive = await PreflightTestHost.ArchiveFor(name, content, new Manual {Url = url, Prompt = "Press download"});
        return new ManualQueueItem(archive, new ManualDownloadTarget(url, "Example", "Press download"),
            "Example downloads need a browser");
    }

    private PreflightContext ContextWithQueue(bool wait, IManualDownloadAcquirer? acquirer, params ManualQueueItem[] queue)
    {
        var archives = queue.Select(q => q.Archive).ToArray();
        _host.Config.ModList.Archives = archives;
        var options = new PreflightOptions
        {
            SendMetrics = false, WaitForManualDownloads = wait, WatchFolder = _watch
        };
        var ctx = _host.Context(options, acquirer);
        ctx.State.RequiredArchives = archives;
        ctx.State.Missing = archives.ToList();
        ctx.State.ManualQueue = queue.ToList();
        ctx.State.RemainingDownloadBytes = archives.Sum(a => a.Size);
        return ctx;
    }

    [Fact]
    public async Task PassesWhenNothingIsManual()
    {
        var ctx = ContextWithQueue(true, null);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Empty(_host.Acquirer.Snapshot());
        Assert.Empty(_progress.ManualQueues);
    }

    [Fact]
    public async Task NeedsUserWithListWhenNotWaiting()
    {
        var one = await Queued("one.7z", "first by hand");
        var two = await Queued("two.7z", "second by hand");
        var ctx = ContextWithQueue(false, null, one, two);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.StartsWith("2 files must be downloaded by hand", result.Message);
        Assert.Contains(PreflightAction.Rescan, result.Actions!);
        Assert.Contains("one.7z", result.Detail);
        Assert.Contains("https://example.invalid/two.7z", result.Detail);
        Assert.Contains("Press download", result.Detail);
        Assert.Contains(one.Archive.Size.ToFileSizeString(), result.Detail);
        Assert.Equal(2, ctx.State.Missing.Count);
        Assert.Equal(one.Archive.Size + two.Archive.Size, ctx.State.RemainingDownloadBytes);

        // The acquirer was stopped: a file landing now is not picked up.
        await PreflightTestHost.WriteFile(_watch.Combine("one.7z"), "first by hand");
        await PreflightTestFolder.SettleTime();
        Assert.Equal(ManualDownloadState.Pending, _host.Acquirer.Snapshot().Single(i => i.Key == "one.7z").State);
        Assert.False(_host.Config.Downloads.Combine("one.7z").FileExists());
    }

    [Fact]
    public async Task CompletedQueueItemBecomesPresentAndWritesMeta()
    {
        var item = await Queued("done.7z", "downloaded by hand");
        var ctx = ContextWithQueue(true, null, item);

        var run = _check.Run(ctx, _progress, CancellationToken.None);
        await PreflightTestFolder.WaitUntil(() => _host.Acquirer.Snapshot().Count == 1, "the acquirer to start");
        await PreflightTestHost.WriteFile(_watch.Combine("done.7z"), "downloaded by hand");

        var result = await run;

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal("1 file downloaded by hand and verified", result.Message);
        var dest = _host.Config.Downloads.Combine("done.7z");
        Assert.True(dest.FileExists());
        Assert.Equal(dest, ctx.State.HashedArchives["done.7z"]);
        Assert.Empty(ctx.State.Missing);
        Assert.Equal(0, ctx.State.RemainingDownloadBytes);

        var meta = dest.WithExtension(Ext.Meta).ReadAllLines().ToList();
        Assert.Equal("[General]", meta[0]);
        Assert.Contains("installed=true", meta);
        Assert.Contains(meta, l => l.StartsWith("manualURL="));
        Assert.Equal(item.Archive.Hash, await _host.Cache.TryGetHashCache(dest));

        Assert.Equal(ArchiveState.Present, _progress.LastStates()["done.7z"]);
        Assert.Contains(_progress.Archives, a => a.State == ArchiveState.ManualRequired);
    }

    [Fact]
    public async Task QueueResultWithWrongHashStaysMissing()
    {
        var item = await Queued("wrong.7z", "what the list expects");
        var placed = await PreflightTestHost.WriteFile(_host.Config.Downloads.Combine("wrong.7z"), "what landed");
        var acquirer = new FakeManualDownloadAcquirer();
        acquirer.Results["wrong.7z"] = (ManualDownloadState.Moved, placed);
        var ctx = ContextWithQueue(true, acquirer, item);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.False(ctx.State.HashedArchives.ContainsKey("wrong.7z"));
        Assert.Single(ctx.State.Missing);
        Assert.Equal(item.Archive.Size, ctx.State.RemainingDownloadBytes);
        Assert.Equal(ArchiveState.Failed, _progress.LastStates()["wrong.7z"]);
        Assert.Contains("does not match", _progress.LastMessage("wrong.7z"));
        Assert.Equal(1, acquirer.StopCalls);
    }

    [Fact]
    public async Task VerifiedQueueResultBecomesPresent()
    {
        var item = await Queued("right.7z", "what the list expects");
        var placed = await PreflightTestHost.WriteFile(_host.Config.Downloads.Combine("right.7z"), "what the list expects");
        var acquirer = new FakeManualDownloadAcquirer();
        acquirer.Results["right.7z"] = (ManualDownloadState.Moved, placed);
        var ctx = ContextWithQueue(false, acquirer, item);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(placed, ctx.State.HashedArchives["right.7z"]);
        Assert.Equal(ArchiveState.Present, _progress.LastStates()["right.7z"]);
        Assert.Equal(_watch, acquirer.WatchFolder);
        Assert.Equal(_host.Config.Downloads, acquirer.DestinationFolder);
    }

    [Fact]
    public async Task PublishesManualQueueChanged()
    {
        var one = await Queued("one.7z", "first by hand");
        var two = await Queued("two.7z", "second by hand");
        var ctx = ContextWithQueue(false, new FakeManualDownloadAcquirer(), one, two);

        var runner = new PreflightRunner(new IPreflightCheck[]
        {
            new FakeCheck(PreflightCheckIds.NexusLogin, 100),
            new FakeCheck(PreflightCheckIds.ArchiveInventory, 400),
            new FakeCheck(PreflightCheckIds.UnsupportedArchives, 500), _check
        }, ctx);
        var events = new List<PreflightEvent>();
        runner.Changed += e =>
        {
            lock (events)
            {
                events.Add(e);
            }
        };

        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        var published = Assert.Single(events.OfType<ManualQueueChanged>());
        Assert.Equal(new[] {"one.7z", "two.7z"}, published.Queue.Select(q => q.Archive.Name));
        Assert.Equal(one.Target, published.Queue[0].Target);
        Assert.Equal(two.Target, runner.Archives["two.7z"].Target);
        Assert.Equal(ArchiveState.ManualRequired, runner.Archives["one.7z"].State);
        Assert.Equal(PreflightState.NeedsUser, runner.Checks.Single(c => c.Id == PreflightCheckIds.ManualDownloads).State);
    }

    [Fact]
    public async Task UnhashableArchivesAreReportedNotThrown()
    {
        var good = await Queued("good.7z", "has a hash and a size");
        var noHash = await Queued("nohash.7z", "no hash");
        noHash.Archive.Hash = default;
        var noSize = await Queued("nosize.7z", "no size");
        noSize.Archive.Size = 0;
        var ctx = ContextWithQueue(false, null, good, noHash, noSize);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.StartsWith("1 file must be downloaded by hand", result.Message);
        Assert.Contains("2 cannot be verified and are unsupported", result.Message);
        Assert.Contains("good.7z", result.Detail);
        Assert.Contains("nohash.7z", result.Detail);
        Assert.Contains("nosize.7z", result.Detail);

        var states = _progress.LastStates();
        Assert.Equal(ArchiveState.ManualRequired, states["good.7z"]);
        Assert.Equal(ArchiveState.Unsupported, states["nohash.7z"]);
        Assert.Equal(ArchiveState.Unsupported, states["nosize.7z"]);
        Assert.Contains("no hash", _progress.LastMessage("nohash.7z"));
        Assert.Contains("no size", _progress.LastMessage("nosize.7z"));

        // Only what can be verified reached the acquirer and the published queue.
        Assert.Equal(new[] {"good.7z"}, _host.Acquirer.Snapshot().Select(i => i.Key));
        Assert.Equal(new[] {"good.7z"}, Assert.Single(_progress.ManualQueues).Select(q => q.Archive.Name));
    }
}
