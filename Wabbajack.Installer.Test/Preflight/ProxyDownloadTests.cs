using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
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
    private readonly IServiceProvider _provider;
    private readonly PreflightTestHost _host;
    private readonly RecordingProgress _progress = new();
    private readonly string _run = Guid.NewGuid().ToString("N");

    public ProxyDownloadTests(IServiceProvider provider)
    {
        _provider = provider;
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
        return ContextWithMissing(null, missing);
    }

    /// <param name="metrics">When given, metrics are on and go to this client instead of the build server.</param>
    private PreflightContext ContextWithMissing(Client? metrics, params Archive[] missing)
    {
        _host.Config.ModList.Archives = missing;
        var options = new PreflightOptions
        {
            SendMetrics = metrics != null, WaitForManualDownloads = false,
            WatchFolder = _host.Manager.CreateFolder().Path, DownloadRetryDelay = TimeSpan.FromMilliseconds(20)
        };
        var ctx = _host.Context(options, wjClient: metrics);
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

    /// <summary>The mirror is the Wabbajack team's own copy, and it has its turn before the proxy.</summary>
    [Fact]
    public async Task MirrorIsPreferredToTheProxy()
    {
        var archive = await Mega("mirrored.7z", "mirrored bytes");
        var proxy = Proxy(archive, "mirrored bytes");
        var mirror = new Http {Url = new Uri($"https://mirror.invalid/{_run}/mirrored.7z")};
        _host.Server.Serve(mirror, Encoding.UTF8.GetBytes("mirrored bytes"));
        _host.Policy.MirrorArchives.Add(new Archive {Name = archive.Name, Hash = archive.Hash, Size = archive.Size, State = mirror});
        var ctx = ContextWithMissing(archive);

        var (_, automated) = await RunDownloadChecks(ctx);

        Assert.Equal("1 downloaded", automated.Message);
        Assert.Same(mirror, archive.State);
        Assert.Equal(1, _host.Server.Attempts(mirror));
        Assert.Equal(0, _host.Server.Attempts(proxy));
        Assert.Empty(ctx.State.Proxied);
    }

    /// <summary>
    ///     Bytes from the proxy that do not hash to what the list expects are thrown away like any other, and the
    ///     user is sent to where the file really lives rather than told the download failed.
    /// </summary>
    [Fact]
    public async Task ProxyCopyThatDoesNotMatchFallsBackToTheOriginalPage()
    {
        var archive = await Mega("spoiled.7z", "the right bytes");
        var proxy = Proxy(archive, "not the right bytes");
        var ctx = ContextWithMissing(archive);

        var (_, automated) = await RunDownloadChecks(ctx);

        Assert.Equal(PreflightState.NeedsUser, automated.State);
        Assert.Equal(ArchiveDownloadPipeline.HashMismatchAttempts, _host.Server.Attempts(proxy));
        var queued = Assert.Single(ctx.State.ManualQueue);
        Assert.Equal("mega.nz", queued.Target.Url.Host);
        Assert.Contains("did not match", queued.Reason);
        Assert.IsType<Mega>(archive.State);
        Assert.False(_host.Config.Downloads.Combine("spoiled.7z").FileExists());
    }

    /// <summary>
    ///     Re-running archive-inventory throws the plan away. The rewritten state survives that, the way the mirror's
    ///     does, so the second plan still downloads from the proxy and the .meta still names the original source.
    /// </summary>
    [Fact]
    public async Task APlanComputedAgainStillUsesTheProxy()
    {
        var archive = await Mega("replanned.7z", "replanned bytes");
        var proxy = Proxy(archive, "replanned bytes");
        var ctx = ContextWithMissing(archive);
        await new ManualDownloadsCheck().Run(ctx, _progress, CancellationToken.None);

        ctx.State.RequiredArchives = ctx.State.RequiredArchives;
        var (manual, automated) = await RunDownloadChecks(ctx);

        Assert.Equal(PreflightState.Passed, manual.State);
        Assert.Equal("1 downloaded", automated.Message);
        Assert.Equal(1, _host.Server.Attempts(proxy));
        var meta = _host.Config.Downloads.Combine("replanned.7z").WithExtension(Ext.Meta).ReadAllLines().ToArray();
        Assert.Contains(meta, l => l.Contains("mega.nz"));
    }

    /// <summary>Two archives can share a hash under different names; the proxy keeps track of each by name.</summary>
    [Fact]
    public async Task ArchivesSharingAHashAreEachDownloaded()
    {
        var first = await Mega("first copy.7z", "shared bytes");
        var second = await Mega("second copy.7z", "shared bytes");
        Proxy(first, "shared bytes");
        var ctx = ContextWithMissing(first, second);

        var (manual, automated) = await RunDownloadChecks(ctx);

        Assert.Equal(PreflightState.Passed, manual.State);
        Assert.Equal("2 downloaded", automated.Message);
        Assert.True(ctx.State.HashedArchives.ContainsKey("first copy.7z"));
        Assert.True(ctx.State.HashedArchives.ContainsKey("second copy.7z"));
    }

    /// <summary>
    ///     The proxy's usage report, which decides what to drop when it runs short of space, counts these. The hash
    ///     is sent as hex because that is how the proxy names its files.
    /// </summary>
    [Fact]
    public async Task EachProxiedDownloadIsCounted()
    {
        var archive = await Mega("counted.7z", "counted bytes");
        Proxy(archive, "counted bytes");
        var server = new BuildServer();
        var ctx = ContextWithMissing(server.Client(_provider), archive);

        await RunDownloadChecks(ctx);

        await server.WaitFor($"metrics/proxy_download/{archive.Hash.ToHex()}");
    }

    [Fact]
    public async Task EachFallbackFromTheProxyIsCounted()
    {
        var archive = await Mega("refused.7z", "refused bytes");
        var proxy = Proxy(archive, "refused bytes");
        _host.Server.FailNext(proxy, ArchiveDownloadPipeline.TransientAttempts,
            () => new Networking.Http.HttpException(503, "Service Unavailable"));
        var server = new BuildServer();
        var ctx = ContextWithMissing(server.Client(_provider), archive);

        await RunDownloadChecks(ctx);

        await server.WaitFor($"metrics/proxy_failed/{archive.Hash.ToHex()}");
        Assert.DoesNotContain(server.Paths, p => p.Contains("proxy_download"));
    }

    /// <summary>Stands in for the build server's metrics endpoint, which every metric goes to.</summary>
    private sealed class BuildServer : HttpMessageHandler, ITokenProvider<WabbajackApiState>
    {
        public ConcurrentQueue<string> Paths { get; } = new();

        public Client Client(IServiceProvider provider)
        {
            return new Client(NullLogger<Client>.Instance, new HttpClient(this), this,
                provider.GetRequiredService<DTOSerializer>(), new Resource<HttpClient>("http", 4),
                new Resource<Client>("hash", 4), new Networking.WabbajackClientApi.Configuration());
        }

        /// <summary>Metrics are sent without waiting for them, so the test waits for the request instead.</summary>
        public async Task WaitFor(string path)
        {
            for (var tries = 0; tries < 200 && !Paths.Any(p => p.EndsWith(path)); tries++)
                await Task.Delay(25);
            Assert.Contains(Paths, p => p.EndsWith(path));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Paths.Enqueue(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("{}")});
        }

        public ValueTask<WabbajackApiState?> Get() =>
            ValueTask.FromResult<WabbajackApiState?>(new WabbajackApiState {MetricsKey = "metrics-key-for-tests"});

        public ValueTask SetToken(WabbajackApiState val) => ValueTask.CompletedTask;
        public ValueTask<bool> Delete() => ValueTask.FromResult(false);
        public bool HaveToken() => true;
    }
}
