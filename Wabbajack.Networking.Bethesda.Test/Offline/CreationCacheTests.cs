using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test.Offline;

/// <summary>
///     <see cref="CreationCache" /> on its own: what it trusts, what it shares between callers, what it
///     cleans up, and what it does after something on the way failed. A Creation is tens of megabytes, so
///     each of these is either a download a user pays for twice or a folder left behind on their disk.
/// </summary>
public class CreationCacheTests : IDisposable
{
    private const long ArcaneArcher = 5648;
    private const string Plugin = "ccBGSSSE002-ExoticArrows.esl";
    private const string Archive = "ccBGSSSE002-ExoticArrows.bsa";

    private static readonly Uri ArcaneArcherUrl =
        new($"https://ugcmods.bethesda.net/public/SKYRIM/client/{ArcaneArcher}/CSV2/CSV2.ckm?versionId=x");

    private readonly FakeDownloads _downloads = new();
    private readonly TemporaryFileManager _temp;

    public CreationCacheTests()
    {
        // A root of its own, for the reason CreationRestorerTests gives: a default manager deletes the
        // shared "temp" folder on dispose, which takes other test classes' files with it.
        _temp = new TemporaryFileManager(KnownFolders.EntryPoint.Combine("creation-cache-test",
            Guid.NewGuid().ToString("N")));
    }

    public void Dispose()
    {
        _temp.Dispose();
        _downloads.Dispose();
    }

    private CreationCache Cache(BethesdaApiClient api)
    {
        return new CreationCache(NullLogger<CreationCache>.Instance, api, CreationIndex.Default,
            new HttpClient(_downloads), _temp);
    }

    private static byte[] Container()
    {
        return new BtarBuilder()
            .With($"Data\\{Plugin}", "plugin bytes")
            .With($"Data\\{Archive}", "archive bytes")
            .Build();
    }

    private static string Md5(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>An API that resolves Arcane Archer to the container <see cref="_downloads" /> serves.</summary>
    private FakeApi Publishing(string? checksum = null)
    {
        var container = Container();
        _downloads.Body = container;
        var api = new FakeApi();
        api.Slots.Add(new CkmSlot(ArcaneArcher, "Arcane Archer Pack", ArcaneArcherUrl, checksum ?? Md5(container),
            container.Length));
        return api;
    }

    /// <summary>
    ///     Some records carry a multipart etag rather than an MD5. There is nothing to check it against, so
    ///     the container is unpacked unverified rather than refused - refusing it would make that Creation
    ///     impossible to fetch at all. <c>Archive.Hash</c> still decides whether the file is the right one.
    /// </summary>
    [Fact]
    public async Task AContainerWithAnEtagForAChecksumIsUnpackedUnverified()
    {
        using var cache = Cache(Publishing("9b2cf535f27731c974343645a3985328-3"));

        var creation = await cache.Get(ArcaneArcher, CancellationToken.None);

        Assert.NotNull(creation);
        Assert.Equal(new[] {Archive, Plugin}, creation!.Files.Select(f => f.ToString()).OrderBy(f => f));
    }

    /// <summary>
    ///     Two files of one Creation asked for at the same moment are one download. Without the lock both
    ///     callers would miss the cache and fetch the same container.
    /// </summary>
    [Fact]
    public async Task TwoCallersAskingForTheSameCreationAtOnceShareOneDownload()
    {
        using var cache = Cache(Publishing());
        _downloads.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = cache.Get(ArcaneArcher, CancellationToken.None);
        await _downloads.FirstRequest.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = cache.Get(ArcaneArcher, CancellationToken.None);

        _downloads.Gate.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(results[0], results[1]);
        Assert.Equal(1, _downloads.Requests);
        Assert.Equal(1, cache.Downloads);
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    ///     The unpacked files are scratch for the run. Disposing the cache deletes them, so a repair of every
    ///     Creation does not leave a few gigabytes behind on the user's disk.
    /// </summary>
    [Fact]
    public async Task DisposingTheCacheDeletesWhatItUnpacked()
    {
        var cache = Cache(Publishing());
        var creation = await cache.Get(ArcaneArcher, CancellationToken.None);
        Assert.True(creation!.Folder.DirectoryExists());

        cache.Dispose();

        Assert.False(creation.Folder.DirectoryExists());
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task AskingADisposedCacheThrows()
    {
        var cache = Cache(Publishing());
        cache.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.Get(ArcaneArcher, CancellationToken.None));
    }

    /// <summary>
    ///     The container URL is presigned and the storage host checks nothing else, so neither the shared
    ///     edge key nor the user's session goes to it.
    /// </summary>
    [Fact]
    public async Task TheContainerFetchCarriesNoKeyAndNoSession()
    {
        using var cache = Cache(Publishing());

        await cache.Get(ArcaneArcher, CancellationToken.None);

        var headers = Assert.Single(_downloads.Headers);
        Assert.False(headers.ContainsKey("x-bnet-key"));
        Assert.False(headers.ContainsKey("x-session-token"));
        Assert.Equal(BethesdaConstants.UserAgent, headers["user-agent"]);
    }

    /// <summary>
    ///     A sign in that failed - Steam was still starting, say - is tried again on the next file rather
    ///     than remembered as a failure for the rest of the run.
    /// </summary>
    [Fact]
    public async Task AFailedSignInIsTriedAgainOnTheNextFile()
    {
        var api = Publishing();
        api.SignInThrows = () => new BethesdaApiException("not yet", System.Net.HttpStatusCode.ServiceUnavailable);
        using var cache = Cache(api);

        await Assert.ThrowsAsync<BethesdaApiException>(() => cache.Get(ArcaneArcher, CancellationToken.None));
        api.SignInThrows = null;
        var creation = await cache.Get(ArcaneArcher, CancellationToken.None);

        Assert.NotNull(creation);
        Assert.Equal(2, api.SignIns);
        Assert.Equal(1, api.Resolves);
    }

    /// <summary>A resolve that failed is likewise asked again, not cached as "nothing resolved".</summary>
    [Fact]
    public async Task AFailedResolveIsTriedAgainOnTheNextFile()
    {
        var api = Publishing();
        api.ResolveThrowsOnce = () => new BethesdaApiException("busy", System.Net.HttpStatusCode.ServiceUnavailable);
        using var cache = Cache(api);

        await Assert.ThrowsAsync<BethesdaApiException>(() => cache.Get(ArcaneArcher, CancellationToken.None));
        var creation = await cache.Get(ArcaneArcher, CancellationToken.None);

        Assert.NotNull(creation);
        Assert.Equal(2, api.Resolves);
        Assert.Equal(1, _downloads.Requests);
    }

    /// <summary>
    ///     With the real client: when the entitlement update fails, the next file should sign in again (and
    ///     so retry the update) rather than resolving against entitlements that were never brought up to
    ///     date. Otherwise a user who owns the Creations is told they "may not own" them.
    /// </summary>
    [Fact]
    public async Task AfterAFailedEntitlementUpdateTheNextFileSignsInAgain()
    {
        var container = Container();
        _downloads.Body = container;
        var bethesda = new ScriptedBethesda()
            .ThenOk("""{"platform":{"code":2000,"response":{"session_token":"sess-1","username":"u"}}}""")
            .ThenOk("""{"platform":{"code":4000,"message":"entitlements unavailable"}}""")
            .ThenOk("""{"platform":{"code":2000,"response":{"session_token":"sess-2","username":"u"}}}""")
            .ThenOk("""{"platform":{"code":2000,"message":"success"}}""")
            .ThenOk("""{"platform":{"code":2000,"response":{"data":[]}}}""");
        var api = new BethesdaApiClient(new HttpClient(bethesda), new FixedTicket(1, 2, 3),
            NullLogger<BethesdaApiClient>.Instance);
        using var cache = Cache(api);

        await Assert.ThrowsAsync<BethesdaApiException>(() => cache.Get(ArcaneArcher, CancellationToken.None));
        var second = await Record.ExceptionAsync(() => cache.Get(ArcaneArcher, CancellationToken.None));

        // Today the third request is the content resolve, sent with the session whose entitlements failed.
        Assert.Equal("/session/external-login", bethesda.Requests[2].Uri.AbsolutePath);
        Assert.Null(second);
    }
}
