using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths.IO;
using Xunit;

#nullable enable

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     The proxy holds copies of Mega, Google Drive and MediaFire files. A file it holds has to download on its
///     own: manual-downloads runs first and opens a browser page for everything in its queue, so a proxied file
///     that reached the queue would send the user to fetch by hand something the app could have fetched itself.
/// </summary>
public class ProxyDownloadTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly RecordingProgress _progress = new();
    private readonly string _run = Guid.NewGuid().ToString("N");

    public ProxyDownloadTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private Task<Archive> Mega(string name, string content)
    {
        return PreflightTestHost.ArchiveFor(name, content, new Mega {Url = new Uri($"https://mega.nz/file/{_run}{name}#key")});
    }

    /// <summary>Puts <paramref name="archive" /> in the proxy index and serves its bytes from the proxy.</summary>
    private WabbajackCDN Proxy(Archive archive, string content, long? indexedSize = null)
    {
        var index = _host.Policy.ProxyIndexValue;
        index.Files[archive.Hash.ToHex()] = indexedSize ?? archive.Size;
        var state = new WabbajackCDN {Url = index.UrlFor(archive.Hash)};
        _host.Server.Serve(state, Encoding.UTF8.GetBytes(content));
        return state;
    }

    private PreflightContext ContextWithMissing(params Archive[] missing)
    {
        _host.Config.ModList.Archives = missing;
        var options = new PreflightOptions
        {
            SendMetrics = false, WaitForManualDownloads = false, WatchFolder = _host.Manager.CreateFolder().Path
        };
        var ctx = _host.Context(options);
        ctx.State.RequiredArchives = missing;
        ctx.State.Missing = missing.ToList();
        ctx.State.RemainingDownloadBytes = missing.Sum(a => a.Size);
        return ctx;
    }

    private async Task<(PreflightResult Manual, PreflightResult Automated)> RunDownloadChecks(PreflightContext ctx)
    {
        var manual = await new ManualDownloadsCheck().Run(ctx, _progress, CancellationToken.None);
        var automated = await new AutomatedDownloadsCheck().Run(ctx, _progress, CancellationToken.None);
        return (manual, automated);
    }

    [Fact]
    public async Task ProxiedFileDownloadsInsteadOfOpeningABrowser()
    {
        var archive = await Mega("proxied.7z", "proxied bytes");
        var proxy = Proxy(archive, "proxied bytes");
        var ctx = ContextWithMissing(archive);

        var (manual, automated) = await RunDownloadChecks(ctx);

        Assert.Equal(PreflightState.Passed, manual.State);
        Assert.Empty(_progress.ManualQueues);
        Assert.Equal("1 downloaded", automated.Message);
        Assert.Equal(1, _host.Server.Attempts(proxy));
        Assert.True(ctx.State.HashedArchives.ContainsKey("proxied.7z"));
        Assert.DoesNotContain(_progress.Archives, a => a.Archive.Name == "proxied.7z" && a.State == ArchiveState.ManualRequired);
    }

    /// <summary>A list compiled from this install has to point at Mega, never at the proxy.</summary>
    [Fact]
    public async Task MetaRecordsTheOriginalSource()
    {
        var archive = await Mega("meta.7z", "meta bytes");
        Proxy(archive, "meta bytes");
        var ctx = ContextWithMissing(archive);

        await RunDownloadChecks(ctx);

        var meta = _host.Config.Downloads.Combine("meta.7z").WithExtension(Ext.Meta).ReadAllLines().ToArray();
        Assert.Contains(meta, l => l.Contains("mega.nz"));
        Assert.DoesNotContain(meta, l => l.Contains("proxy.invalid"));
    }

    [Fact]
    public async Task FailedProxyDownloadFallsBackToTheOriginalPage()
    {
        var archive = await Mega("broken.7z", "broken bytes");
        var original = archive.State;
        var proxy = Proxy(archive, "broken bytes");
        _host.Server.FailNext(proxy, ArchiveDownloadPipeline.TransientAttempts,
            () => new Networking.Http.HttpException(503, "Service Unavailable"));
        var ctx = ContextWithMissing(archive);

        var (_, automated) = await RunDownloadChecks(ctx);

        Assert.Equal(PreflightState.NeedsUser, automated.State);
        var queued = Assert.Single(ctx.State.ManualQueue);
        Assert.Equal("mega.nz", queued.Target.Url.Host);
        Assert.Contains("proxy", queued.Reason);
        Assert.Same(original, archive.State);
        Assert.False(ctx.State.Proxied.ContainsKey("broken.7z"));
    }

    /// <summary>The index records sizes so an entry can never stand in for another file with the same hash.</summary>
    [Fact]
    public async Task IndexEntryOfAnotherSizeIsNotUsed()
    {
        var archive = await Mega("resized.7z", "resized bytes");
        var proxy = Proxy(archive, "resized bytes", archive.Size + 1);
        var ctx = ContextWithMissing(archive);

        var (manual, _) = await RunDownloadChecks(ctx);

        Assert.Equal(PreflightState.NeedsUser, manual.State);
        Assert.Equal("resized.7z", Assert.Single(ctx.State.ManualQueue).Archive.Name);
        Assert.Equal(0, _host.Server.Attempts(proxy));
    }

    /// <summary>Not reaching the proxy leaves the user where they were without it: those files go to the browser.</summary>
    [Fact]
    public async Task ProxyThatCannotBeReachedLeavesFilesManual()
    {
        var archive = await Mega("offline.7z", "offline bytes");
        Proxy(archive, "offline bytes");
        _host.Policy.ThrowFromProxyIndex = new Networking.Http.HttpException(503, "Service Unavailable");
        var ctx = ContextWithMissing(archive);

        var (manual, _) = await RunDownloadChecks(ctx);

        Assert.Equal(PreflightState.NeedsUser, manual.State);
        Assert.Equal("offline.7z", Assert.Single(ctx.State.ManualQueue).Archive.Name);
    }

    /// <summary>A source the app can already download from is left alone, even if the proxy has its file.</summary>
    [Fact]
    public async Task AutomatedSourcesAreNotProxied()
    {
        var archive = await PreflightTestHost.ArchiveFor("http.7z", "http bytes",
            new Http {Url = new Uri($"https://example.invalid/{_run}/http.7z")});
        _host.Server.Serve(archive.State, Encoding.UTF8.GetBytes("http bytes"));
        var proxy = Proxy(archive, "http bytes");
        var ctx = ContextWithMissing(archive);

        await RunDownloadChecks(ctx);

        Assert.Equal(1, _host.Server.Attempts(archive.State));
        Assert.Equal(0, _host.Server.Attempts(proxy));
        Assert.Empty(ctx.State.Proxied);
    }

    /// <summary>One index answers for the whole run, rather than a request per archive.</summary>
    [Fact]
    public async Task IndexIsReadOncePerRun()
    {
        var first = await Mega("first.7z", "first bytes");
        var second = await Mega("second.7z", "second bytes");
        Proxy(first, "first bytes");
        Proxy(second, "second bytes");
        var ctx = ContextWithMissing(first, second);

        var (_, automated) = await RunDownloadChecks(ctx);

        Assert.Equal("2 downloaded", automated.Message);
        Assert.Equal(1, _host.Policy.ProxyIndexCalls);
    }
}
