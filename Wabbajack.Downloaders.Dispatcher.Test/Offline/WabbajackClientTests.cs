#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;
using ClientConfiguration = Wabbajack.Networking.WabbajackClientApi.Configuration;

namespace Wabbajack.Downloaders.Dispatcher.Test.Offline;

/// <summary>
///     The Wabbajack client against <see cref="FakeHttp" />: what it reads from the build server and the GitHub-hosted
///     lists, what it sends with each request, and the shape of the author uploads. Requests that go through Octokit
///     (collection mappings, publishing a list) use Octokit's own client and are not covered here.
/// </summary>
public class WabbajackClientTests : IAsyncLifetime
{
    private static readonly Uri BuildServer = new ClientConfiguration().BuildServerUrl;

    private readonly DTOSerializer _dtos;
    private readonly TemporaryFileManager _manager;
    private TemporaryPath _folder;

    public WabbajackClientTests(DTOSerializer dtos, TemporaryFileManager manager)
    {
        _dtos = dtos;
        _manager = manager;
    }

    public Task InitializeAsync()
    {
        _folder = _manager.CreateFolder();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _folder.DisposeAsync();
    }

    private Client Client(FakeHttp http, string? authorKey = null)
    {
        return Client(http.Client(), authorKey);
    }

    private Client Client(HttpClient http, string? authorKey = null)
    {
        return new Client(NullLogger<Client>.Instance, http, new Keys(authorKey), _dtos,
            new Resource<HttpClient>("http", 4), new Resource<Client>("hash", 4), new ClientConfiguration());
    }

    private static HttpResponseMessage Text(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) {Content = new StringContent(body, Encoding.UTF8, "application/json")};
    }

    private static Archive Archive(string name, ulong hash)
    {
        return new Archive
        {
            Name = name, Hash = Hash.FromULong(hash), Size = 1234,
            State = new DTOs.DownloadStates.Http {Url = new Uri($"https://example.com/{name}")}
        };
    }

    [Fact]
    public async Task AllowLists_AreReadFromYaml()
    {
        var yaml = "AllowedPrefixes:\n  - https://files.example.com/\nGoogleIDs:\n  - abc123\n";
        var configuration = new ClientConfiguration();
        var http = new FakeHttp(request =>
            request.RequestUri == configuration.ServerAllowList || request.RequestUri == configuration.MirrorAllowList
                ? Text(yaml)
                : Text("", HttpStatusCode.NotFound));
        var client = Client(http);

        foreach (var list in new[] {await client.LoadDownloadAllowList(), await client.LoadMirrorAllowList()})
        {
            Assert.Equal(new[] {"https://files.example.com/"}, list.AllowedPrefixes);
            Assert.Equal(new[] {"abc123"}, list.GoogleIDs);
        }
    }

    [Fact]
    public async Task MirrorList_IsReadUnlessTheClientIsToldToIgnoreIt()
    {
        var mirrors = new[] {Archive("a.7z", 1), Archive("b.7z", 2)};
        var http = new FakeHttp(_ => Text(_dtos.Serialize(mirrors)));
        var client = Client(http);

        Assert.Equal(new[] {"a.7z", "b.7z"}, (await client.LoadMirrors()).Select(a => a.Name));

        client.IgnoreMirrorList = true;
        Assert.Empty(await client.LoadMirrors());
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task UpgradedArchives_AreKeyedByTheOriginalsHash()
    {
        var upgrades = new[]
        {
            new ValidatedArchive {Original = Archive("old.7z", 10), PatchedFrom = Archive("new.7z", 11)}
        };
        var client = Client(new FakeHttp(_ => Text(_dtos.Serialize(upgrades))));

        var byHash = await client.LoadUpgradedArchives();

        Assert.Equal("new.7z", byHash[Hash.FromULong(10)].PatchedFrom!.Name);
    }

    [Fact]
    public async Task GameArchives_ComeFromTheIndexedGameFilesRepository()
    {
        var http = new FakeHttp(_ => Text(_dtos.Serialize(new[] {Archive("Skyrim.esm", 5)})));

        var archives = await Client(http).GetGameArchives(Game.SkyrimSpecialEdition, "1.5.97.0");

        Assert.Equal("Skyrim.esm", Assert.Single(archives).Name);
        Assert.Equal(
            "https://raw.githubusercontent.com/wabbajack-tools/indexed-game-files/master/SkyrimSpecialEdition/1.5.97.0.json",
            Assert.Single(http.Requests).RequestUri!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("author-key")]
    public async Task BuildServerRequests_CarryTheMetricsKey_AndTheAuthorKeyWhenThereIsOne(string? authorKey)
    {
        var configuration = new ClientConfiguration();
        var http = new FakeHttp(_ => Text("[]"));

        await Client(http, authorKey).GetArchivesForHash(Hash.FromULong(0xABCDEF));

        var request = Assert.Single(http.Requests);
        Assert.Equal($"{BuildServer}mod_files/by_hash/{Hash.FromULong(0xABCDEF).ToHex()}", request.RequestUri!.ToString());
        Assert.Equal(Keys.MetricsKey, request.Headers.GetValues(configuration.MetricsKeyHeader).Single());
        Assert.Equal(authorKey != null, request.Headers.Contains(configuration.AuthorKeyHeader));
        if (authorKey != null)
            Assert.Equal(authorKey, request.Headers.GetValues(configuration.AuthorKeyHeader).Single());
    }

    [Fact]
    public async Task Metric_GoesToItsActionAndSubject()
    {
        var http = new FakeHttp(_ => Text(""));

        await Client(http).SendMetric("downloading", "Some List");

        var request = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{BuildServer}metrics/downloading/Some%20List", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ModListDefinition_IsSentGzipped()
    {
        byte[]? body = null;
        var http = new FakeHttp(request =>
        {
            body = request.Content!.ReadAsByteArrayAsync().Result;
            return Text("");
        });
        var list = new ModList {Name = "Gzipped List", Archives = new[] {Archive("a.7z", 1)}};

        await Client(http).SendModListDefinition(list);

        var request = Assert.Single(http.Requests);
        Assert.Equal($"{BuildServer}list_definitions/ingest", request.RequestUri!.ToString());
        Assert.Equal("gzip", request.Headers.GetValues("x-compressed-body").Single());
        await using var gz = new GZipStream(new MemoryStream(body!), CompressionMode.Decompress);
        var sent = await _dtos.DeserializeAsync<ModList>(gz);
        Assert.Equal("Gzipped List", sent!.Name);
        Assert.Equal("a.7z", Assert.Single(sent.Archives).Name);
    }

    /// <summary>
    ///     The gallery is every repository's lists together. A repository that serves something unreadable is
    ///     left out rather than emptying the gallery, and a list is official if its repository is the featured
    ///     one or the featured list names it.
    /// </summary>
    [Fact]
    public async Task Gallery_GathersEveryRepository_AndMarksTheFeaturedOnes()
    {
        string Lists(params string[] machineUrls) => _dtos.Serialize(machineUrls
            .Select(m => new ModlistMetadata {Title = m, Links = new LinksObject {MachineURL = m}}).ToArray());

        var http = new FakeHttp(request => request.RequestUri!.AbsolutePath switch
        {
            var p when p.EndsWith("/repositories.json") => Text(
                "{\"wj-featured\":\"https://repo.example/featured.json\",\"alice\":\"https://repo.example/alice.json\",\"broken\":\"https://repo.example/broken.json\"}"),
            var p when p.EndsWith("/featured_lists.json") => Text("[\"alice/second\"]"),
            "/featured.json" => Text(Lists("star")),
            "/alice.json" => Text(Lists("first", "second")),
            "/broken.json" => Text("<html>not json</html>"),
            _ => Text("", HttpStatusCode.NotFound)
        });

        var lists = await Client(http).LoadLists();

        Assert.Equal(new[] {"alice/first", "alice/second", "wj-featured/star"},
            lists.Select(l => l.NamespacedName).OrderBy(n => n));
        Assert.Equal(new[] {"alice/second", "wj-featured/star"},
            lists.Where(l => l.Official).Select(l => l.NamespacedName).OrderBy(n => n));
    }

    [Fact]
    public async Task AllowedTags_AreCaseInsensitive()
    {
        var client = Client(new FakeHttp(_ => Text("[\"Graphics\",\"Gameplay\"]")));

        var tags = await client.LoadAllowedTags();

        Assert.Contains("graphics", tags);
        Assert.Contains("GAMEPLAY", tags);
    }

    [Fact]
    public async Task FileDefinition_SplitsTheFileIntoHashedTwoMegabyteParts()
    {
        var file = _folder.Path.Combine("big.bin");
        var data = Enumerable.Range(0, (int) Wabbajack.Networking.WabbajackClientApi.Client.UploadedFileBlockSize * 2 + 1000)
            .Select(i => (byte) (i * 13 % 251)).ToArray();
        await file.WriteAllBytesAsync(data);

        var definition = await Client(new FakeHttp(_ => Text(""))).GenerateFileDefinition(file);

        Assert.Equal(data.Length, definition.Size);
        Assert.Equal(await data.Hash(), definition.Hash);
        Assert.Equal(new long[] {0, 2 << 20, 4 << 20}, definition.Parts.Select(p => p.Offset));
        Assert.Equal(new long[] {2 << 20, 2 << 20, 1000}, definition.Parts.Select(p => p.Size));
        foreach (var part in definition.Parts)
            Assert.Equal(await data.AsSpan((int) part.Offset, (int) part.Size).ToArray().Hash(), part.Hash);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task ProxyUrl_IsOfferedOnlyWhenTheProxyHasTheFile(HttpStatusCode status, bool offered)
    {
        var http = new FakeHttp(request => new HttpResponseMessage(status));
        var archive = Archive("a.7z", 7);

        var url = await Client(http).MakeProxyUrl(archive, new Uri("https://files.example.com/a.7z?x=1"));

        Assert.Equal(HttpMethod.Head, Assert.Single(http.Requests).Method);
        Assert.Equal(offered, url != null);
        if (offered)
            Assert.Equal($"{BuildServer}proxy?name=a.7z&hash={archive.Hash.ToHex()}&uri=https%3a%2f%2ffiles.example.com%2fa.7z%3fx%3d1",
                url!.ToString());
    }

    [Fact]
    public async Task ProxyThatCannotBeReached_IsNotOffered()
    {
        var http = new FakeHttp(_ => throw new HttpRequestException("no route"));

        Assert.Null(await Client(http).MakeProxyUrl(Archive("a.7z", 7), new Uri("https://files.example.com/a.7z")));
    }

    [Fact]
    public void MirrorAndPatchUrls_NameTheHashes()
    {
        var client = Client(new FakeHttp(_ => Text("")));
        var a = Hash.FromULong(1);
        var b = Hash.FromULong(2);

        Assert.Equal($"https://mirror.wabbajack.org/{a.ToHex()}", client.GetMirrorUrl(a).ToString());
        Assert.Equal($"https://patches.wabbajack.org/{a.ToHex()}_{b.ToHex()}", client.GetPatchUrl(a, b).ToString());
    }

    /// <summary>The server echoes each block's hash back; a patch is only trusted when every one matches.</summary>
    [Fact]
    public async Task Patch_IsUploadedInBlocksTheServerConfirms()
    {
        var data = Enumerable.Range(0, (int) Wabbajack.Networking.WabbajackClientApi.Client.UploadedFileBlockSize + 500)
            .Select(i => (byte) i).ToArray();
        var received = new ConcurrentDictionary<long, byte[]>();
        var http = new FakeHttp(request =>
        {
            var bytes = request.Content!.ReadAsByteArrayAsync().Result;
            var start = long.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["start"]!);
            received[start] = bytes;
            return Text(bytes.Hash().Result.ToHex());
        });
        var validated = new ValidatedArchive {Original = Archive("old.7z", 10), PatchedFrom = Archive("new.7z", 11)};

        var result = await Client(http).UploadPatch(validated, new MemoryStream(data));

        Assert.Equal(data, received.OrderBy(kv => kv.Key).SelectMany(kv => kv.Value).ToArray());
        Assert.Equal($"https://patches.wabbajack.org/{Hash.FromULong(10).ToHex()}_{Hash.FromULong(11).ToHex()}",
            result.PatchUrl!.ToString());
    }

    [Fact]
    public async Task Patch_BlockTheServerDidNotReceiveIntact_Fails()
    {
        var http = new FakeHttp(_ => Text(Hash.FromULong(999).ToHex()));
        var validated = new ValidatedArchive {Original = Archive("old.7z", 10), PatchedFrom = Archive("new.7z", 11)};

        var error = await Assert.ThrowsAsync<Exception>(() =>
            Client(http).UploadPatch(validated, new MemoryStream(new byte[100])));

        Assert.Contains("does not match", error.Message);
    }

    [Fact]
    public async Task Mirror_IsCreatedThenUploadedPartByPartThenFinished()
    {
        var file = _folder.Path.Combine("mirror.7z");
        await file.WriteAllBytesAsync(Enumerable.Range(0, (2 << 20) + 10).Select(i => (byte) i).ToArray());
        var http = new FakeHttp(_ => Text(""));
        var client = Client(http, "author-key");
        var definition = await client.GenerateFileDefinition(file);
        var hex = definition.Hash.ToHex();

        await client.UploadMirror(definition, file);

        Assert.Equal(new[]
        {
            $"PUT {BuildServer}mirrored_files/create/{hex}",
            $"PUT {BuildServer}mirrored_files/{hex}/part/0",
            $"PUT {BuildServer}mirrored_files/{hex}/part/1",
            $"PUT {BuildServer}mirrored_files/{hex}/finish"
        }, http.Requests.Select(r => $"{r.Method} {r.RequestUri}"));
    }

    [Fact]
    public async Task AuthorFile_IsCreatedUploadedAndFinished_UnderTheIdTheServerAssigns()
    {
        var file = _folder.Path.Combine("My List.wabbajack");
        var data = Enumerable.Range(0, (2 << 20) + 10).Select(i => (byte) (i % 7)).ToArray();
        await file.WriteAllBytesAsync(data);
        var http = new FakeHttp(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/authored_files/create")) return Text("My List.wabbajack_abcd");
            if (path.Contains("/part/"))
                return Text(request.Content!.ReadAsByteArrayAsync().Result.Hash().Result.ToBase64());
            return Text("");
        });

        var (_, upload) = await Client(http, "author-key").UploadAuthorFile(file);
        var url = await upload;

        Assert.Equal("https://authored-files.wabbajack.org/My List.wabbajack_My List.wabbajack_abcd", url.ToString());
        var paths = http.Requests.Select(r => r.RequestUri!.AbsolutePath).ToList();
        Assert.Equal("/authored_files/create", paths.First());
        Assert.Equal(2, paths.Count(p => p.StartsWith("/authored_files/My%20List.wabbajack_abcd/part/")));
        Assert.Equal("/authored_files/My%20List.wabbajack_abcd/finish", paths.Last());
    }

    [Fact]
    public async Task MyModlists_FailureFromTheServer_IsAnHttpException()
    {
        var http = new FakeHttp(_ => Text("", HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpException>(() => Client(http, "author-key").GetMyModlists(CancellationToken.None));
    }

    /// <summary>
    ///     Forced healing is a GitHub file the build server reads and writes for the client: read with its sha,
    ///     then written back with the patch appended and the sha it was read at.
    /// </summary>
    [Fact]
    public async Task ForceHealedPatch_IsAppendedToTheFileTheServerHolds()
    {
        var existing = new[] {new ValidatedArchive {Original = Archive("first.7z", 1)}};
        string? written = null;
        var configuration = new ClientConfiguration();
        var http = new FakeHttp(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                var response = Text(_dtos.Serialize(existing));
                response.Headers.Add(configuration.ResponseShaHeader, "sha-1234");
                return response;
            }

            written = request.Content!.ReadAsStringAsync().Result;
            return Text("");
        });

        await Client(http, "author-key").AddForceHealedPatch(new ValidatedArchive {Original = Archive("second.7z", 2)});

        var post = http.Requests.Last();
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Contains("oldSha=sha-1234", post.RequestUri!.Query);
        Assert.Equal(new[] {"first.7z", "second.7z"},
            _dtos.Deserialize<ValidatedArchive[]>(written!)!.Select(a => a.Original.Name));
    }

    /// <summary>
    ///     Preflight reads the proxy's index once per run to decide which Mega, Google Drive and MediaFire files
    ///     download on their own; the files are then fetched from where the index was found.
    /// </summary>
    [Fact]
    public async Task LoadProxyIndex_ReadsTheIndexAndWhereItsFilesLive()
    {
        var proxy = new ClientConfiguration().ProxyUrl;
        var http = new FakeHttp(_ => Text("""{"Version":1,"Generated":"2026-10-17T23:46:13Z","Files":{"ce40df44e3c6f511":998789120}}"""));

        var index = await Client(http).LoadProxyIndex(CancellationToken.None);

        Assert.Equal(new Uri(proxy, "index.json"), Assert.Single(http.Requests).RequestUri);
        Assert.Equal(proxy, index.BaseUrl);
        Assert.True(index.Holds(Hash.FromHex("ce40df44e3c6f511"), 998789120));
        Assert.Equal(new Uri(proxy, "ce40df44e3c6f511"), index.UrlFor(Hash.FromHex("ce40df44e3c6f511")));
    }

    /// <summary>Preflight treats any failure here as "no proxy", so the failure has to reach it as an exception.</summary>
    [Fact]
    public async Task LoadProxyIndex_ServerErrorThrows()
    {
        var http = new FakeHttp(_ => FakeHttp.Status(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => Client(http).LoadProxyIndex(CancellationToken.None));
    }

    /// <summary>
    ///     A missing index costs the user nothing they had before, so a proxy that never answers must not hold up the
    ///     download plan for the HTTP client's own, much longer, timeout.
    /// </summary>
    [Fact]
    public async Task LoadProxyIndex_GivesUpOnAProxyThatNeverAnswers()
    {
        var client = Client(new HttpClient(new NeverAnswers()));
        client.ProxyIndexTimeout = TimeSpan.FromMilliseconds(100);
        var started = DateTime.UtcNow;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LoadProxyIndex(CancellationToken.None));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task LoadProxyIndex_StopsWhenTheCallerCancels()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client(new HttpClient(new NeverAnswers())).LoadProxyIndex(cancel.Token));
        Assert.True(cancel.IsCancellationRequested);
    }

    private sealed class NeverAnswers : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class Keys(string? authorKey) : ITokenProvider<WabbajackApiState>
    {
        public const string MetricsKey = "metrics-key-for-tests";

        public ValueTask<WabbajackApiState?> Get() =>
            ValueTask.FromResult<WabbajackApiState?>(new WabbajackApiState {MetricsKey = MetricsKey, AuthorKey = authorKey});

        public ValueTask SetToken(WabbajackApiState val) => ValueTask.CompletedTask;
        public ValueTask<bool> Delete() => ValueTask.FromResult(false);
        public bool HaveToken() => true;
    }
}
