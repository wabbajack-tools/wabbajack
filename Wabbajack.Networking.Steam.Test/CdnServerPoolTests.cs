using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     The pool is the only thing standing between a download and a single bad content server. These cover
///     the decisions it makes without one: which servers are usable at all, that it moves on rather than
///     retrying a host that just failed, and that running out of hosts means re-asking Steam.
///     They run against a stand-in rather than SteamKit's <c>Server</c>, whose properties cannot be set
///     from outside the library, which is why the pool is written against
///     <see cref="ContentServerFacts" /> in the first place.
/// </summary>
public class CdnServerPoolTests
{
    private const uint AppId = 489830;

    /// <summary>Skyrim Special Edition's Creation Kit: a second app, in the same repair, on the same run.</summary>
    private const uint ToolAppId = 1946180;

    [Fact]
    public async Task ServersThatWillNotServeThisAppAreNotOffered()
    {
        var pool = Pool(
            Server("cdn-ok.example", "CDN"),
            Server("cache-ok.example", "SteamCache"),
            Server("other-app.example", "CDN", allowedAppIds: new[] {440u}),
            Server("not-a-cdn.example", "OpenCache"));

        var seen = new HashSet<string>();
        for (var i = 0; i < 8; i++) seen.Add((await pool.TakeAsync(AppId, CancellationToken.None)).Host);

        Assert.Equal(new[] {"cache-ok.example", "cdn-ok.example"}, seen.OrderBy(h => h).ToArray());
    }

    [Fact]
    public async Task AServerAllowedForThisAppSpecificallyIsOffered()
    {
        var pool = Pool(Server("ours.example", "CDN", allowedAppIds: new[] {AppId}));

        Assert.Equal("ours.example", (await pool.TakeAsync(AppId, CancellationToken.None)).Host);
    }

    [Fact]
    public async Task TheLoadIsSpreadRatherThanAimedAtOneHost()
    {
        var pool = Pool(
            Server("a.example", "CDN"),
            Server("b.example", "CDN"),
            Server("c.example", "CDN"));

        var seen = new List<string>();
        for (var i = 0; i < 3; i++) seen.Add((await pool.TakeAsync(AppId, CancellationToken.None)).Host);

        Assert.Equal(3, seen.Distinct().Count());
    }

    [Fact]
    public async Task AServerWorthMoreSlotsGetsMoreOfTheWork()
    {
        var pool = Pool(
            Server("heavy.example", "CDN", numEntries: 3),
            Server("light.example", "CDN"));

        var seen = new List<string>();
        for (var i = 0; i < 8; i++) seen.Add((await pool.TakeAsync(AppId, CancellationToken.None)).Host);

        // The weighting, not the order it comes out in. Which slot the cursor happens to start on is an
        // implementation detail, and pinning it would fail the next time the walk changed without anything
        // about the weighting having changed.
        Assert.True(seen.Count(h => h == "heavy.example") > seen.Count(h => h == "light.example"),
            "the server Steam says is worth three slots should get more work than the one worth one");

        // Still used, though. A weighting that starved the lighter server would not be a weighting.
        Assert.Contains("light.example", seen);
    }

    [Fact]
    public async Task AStruckOffServerIsNotOfferedAgain()
    {
        var bad = Server("bad.example", "CDN");
        var pool = Pool(bad, Server("good.example", "CDN"));

        // Take one first so the list exists. A strike is a report about a server that was handed out and
        // then failed, and a rebuild deliberately forgets earlier strikes -- that is what lets the pool come
        // back from having struck off everything.
        await pool.TakeAsync(AppId, CancellationToken.None);
        pool.StrikeOff(bad);

        for (var i = 0; i < 5; i++)
            Assert.Equal("good.example", (await pool.TakeAsync(AppId, CancellationToken.None)).Host);
    }

    [Fact]
    public async Task EverythingFailingReAsksSteamRatherThanGivingUp()
    {
        // A whole region going away is a reason to fetch the directory again, not to stop: the second
        // answer is usually a different set of hosts.
        var fetches = 0;
        var pool = new CdnServerPool<FakeServer>(NullLogger.Instance, null, _ =>
        {
            fetches++;
            return Task.FromResult<IEnumerable<FakeServer>>(new[] {Server($"round{fetches}.example", "CDN")});
        }, Describe);

        var first = await pool.TakeAsync(AppId, CancellationToken.None);
        Assert.Equal("round1.example", first.Host);

        pool.StrikeOff(first);

        Assert.Equal("round2.example", (await pool.TakeAsync(AppId, CancellationToken.None)).Host);
        Assert.Equal(2, fetches);
    }

    /// <summary>
    ///     Steam's directory is the same answer whatever app is named; the app decides only which of those
    ///     servers are kept. A repair alternates between a game's app and its Creation Kit's for every file,
    ///     so a pool that re-asked on each switch spent two round trips per file re-learning a list that had
    ///     not changed.
    /// </summary>
    [Fact]
    public async Task AlternatingBetweenTwoAppsDoesNotReAskSteamEachTime()
    {
        var fetches = 0;
        var pool = new CdnServerPool<FakeServer>(NullLogger.Instance, null, _ =>
        {
            fetches++;
            return Task.FromResult<IEnumerable<FakeServer>>(new[] {Server("serves-both.example", "CDN")});
        }, Describe);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal("serves-both.example", (await pool.TakeAsync(AppId, CancellationToken.None)).Host);
            Assert.Equal("serves-both.example", (await pool.TakeAsync(ToolAppId, CancellationToken.None)).Host);
        }

        Assert.Equal(1, fetches);
    }

    /// <summary>
    ///     Sharing the directory between apps must not share the filter. Steam does hand out servers that
    ///     will serve one app and not another, and offering one of those for the wrong app is a request
    ///     that gets refused.
    /// </summary>
    [Fact]
    public async Task TheFilterIsStillPerAppThoughTheDirectoryIsShared()
    {
        var pool = Pool(
            Server("game-only.example", "CDN", allowedAppIds: new[] {AppId}),
            Server("tool-only.example", "CDN", allowedAppIds: new[] {ToolAppId}));

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal("game-only.example", (await pool.TakeAsync(AppId, CancellationToken.None)).Host);
            Assert.Equal("tool-only.example", (await pool.TakeAsync(ToolAppId, CancellationToken.None)).Host);
        }
    }

    /// <summary>
    ///     And a strike against a host is a strike whichever app was being fetched when it failed: a content
    ///     server that has stopped answering has not stopped answering for one app only.
    /// </summary>
    [Fact]
    public async Task AServerStruckOffWhileFetchingForOneAppIsNotOfferedForAnother()
    {
        var bad = Server("bad.example", "CDN");
        var pool = Pool(bad, Server("good.example", "CDN"));

        await pool.TakeAsync(AppId, CancellationToken.None);
        pool.StrikeOff(bad);

        for (var i = 0; i < 5; i++)
            Assert.Equal("good.example", (await pool.TakeAsync(ToolAppId, CancellationToken.None)).Host);
    }

    [Fact]
    public async Task NothingUsableIsSaidPlainlyRatherThanLoopingForever()
    {
        var pool = Pool(Server("other-app.example", "CDN", allowedAppIds: new[] {440u}));

        await Assert.ThrowsAsync<SteamNoContentServersException>(
            () => pool.TakeAsync(AppId, CancellationToken.None));
    }

    [Fact]
    public async Task TheProxyServerIsPickedOutAndNotHandedOutAsATarget()
    {
        // A UseAsProxy server rewrites requests on the way to a real one; downloading from it directly is
        // not what it is for.
        var pool = Pool(
            Server("proxy.example", "CDN", useAsProxy: true),
            Server("real.example", "CDN"));

        var taken = await pool.TakeAsync(AppId, CancellationToken.None);

        Assert.Equal("proxy.example", pool.ProxyServer?.Host);
        Assert.Equal("real.example", taken.Host);
    }

    private static CdnServerPool<FakeServer> Pool(params FakeServer[] servers)
    {
        return new CdnServerPool<FakeServer>(NullLogger.Instance, null,
            _ => Task.FromResult<IEnumerable<FakeServer>>(servers), Describe);
    }

    private static ContentServerFacts Describe(FakeServer server)
    {
        return new ContentServerFacts(server.Host, server.Type, server.AllowedAppIds, server.NumEntries,
            server.WeightedLoad, server.UseAsProxy);
    }

    private static FakeServer Server(string host, string? type = null, uint[]? allowedAppIds = null,
        bool useAsProxy = false, int numEntries = 1, float load = 1)
    {
        return new FakeServer(host, type, allowedAppIds ?? Array.Empty<uint>(), numEntries, load, useAsProxy);
    }

    private sealed record FakeServer(string Host, string? Type, uint[] AllowedAppIds, int NumEntries,
        float WeightedLoad, bool UseAsProxy);
}
