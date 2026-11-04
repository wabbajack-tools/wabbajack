using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Xunit;
using ClientConfiguration = Wabbajack.Networking.WabbajackClientApi.Configuration;

namespace Wabbajack.Networking.Steam.Test.Offline;

/// <summary>
///     Reading <c>indexed-game-files</c> through the real Wabbajack client, over a scripted HTTP handler.
///     The index is written to disk by <c>GameFileIndexFolder</c> in the CLI and read back over HTTP here, so
///     the two have to agree on every path: a mismatch means every repair of an older game version silently
///     finds nothing, which users would only see as "no record of this version".
/// </summary>
public class ClientSteamManifestIndexTests
{
    private const string IndexRoot = "/wabbajack-tools/indexed-game-files/master/";

    private readonly DTOSerializer _dtos = new(new JsonConverter[] {new HashJsonConverter()});
    private readonly ScriptedHttp _http = new();

    private ClientSteamManifestIndex Index()
    {
        var client = new Client(NullLogger<Client>.Instance, new HttpClient(_http), new NoKeys(), _dtos,
            new Resource<HttpClient>("http", 4), new Resource<Client>("hash", 4), new ClientConfiguration());
        return new ClientSteamManifestIndex(NullLogger<ClientSteamManifestIndex>.Instance, client);
    }

    private HttpResponseMessage Json<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, _dtos.Options), Encoding.UTF8,
                "application/json")
        };
    }

    /// <summary>
    ///     A version's manifests are read from <c>{game}/{version}_steam_manifests.json</c>, the same name
    ///     <c>hash-game-files</c> writes.
    /// </summary>
    [Fact]
    public async Task AVersionIsReadFromTheFileHashGameFilesWrites()
    {
        _http.Respond = _ => Json(new[] {new SteamManifest {Depot = 489833, Manifest = 7}});

        var manifests = await Index().Get(Game.SkyrimSpecialEdition, "1.6.640.0", CancellationToken.None);

        var request = Assert.Single(_http.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("raw.githubusercontent.com", request.Uri.Host);
        Assert.Equal(IndexRoot + "SkyrimSpecialEdition/1.6.640.0_steam_manifests.json", request.Uri.AbsolutePath);
        Assert.EndsWith("/" + GameFileIndex.SteamManifestsFile("1.6.640.0"), request.Uri.AbsolutePath);

        var manifest = Assert.Single(manifests);
        Assert.Equal(489833u, manifest.Depot);
        Assert.Equal(7ul, manifest.Manifest);
    }

    /// <summary>A version nobody indexed is a 404, which is the ordinary "no record" answer and not an error.</summary>
    [Fact]
    public async Task AVersionNobodyIndexedIsEmpty()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        Assert.Empty(await Index().Get(Game.SkyrimSpecialEdition, "1.4.2.0", CancellationToken.None));
        Assert.Single(_http.Requests);
    }

    /// <summary>A list that recorded no version costs no request at all.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankVersionAsksForNothing(string version)
    {
        Assert.Empty(await Index().Get(Game.SkyrimSpecialEdition, version, CancellationToken.None));
        Assert.Empty(_http.Requests);
    }

    /// <summary>
    ///     A server error is not "no record": hiding it would tell the user the version was never indexed
    ///     when the index was simply unreachable.
    /// </summary>
    [Fact]
    public async Task AServerErrorReadingAVersionIsNotHidden()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Index().Get(Game.SkyrimSpecialEdition, "1.6.640.0", CancellationToken.None));
        Assert.Equal(HttpStatusCode.InternalServerError, thrown.StatusCode);
    }

    /// <summary>
    ///     A hash is looked up in the shard named by its first byte, under <c>steam_depots</c>, the layout
    ///     <c>index-steam-depots</c> writes.
    /// </summary>
    [Fact]
    public async Task AHashIsLookedUpInTheShardItsFirstByteNames()
    {
        var hash = Hash.FromHex("ab00000000000001");
        _http.Respond = _ => Json(Array.Empty<IndexedGameFile>());

        await Index().Find(Game.SkyrimSpecialEdition, hash, CancellationToken.None);

        var request = Assert.Single(_http.Requests);
        Assert.Equal(IndexRoot + "SkyrimSpecialEdition/steam_depots/ab.json", request.Uri.AbsolutePath);
        Assert.EndsWith("/" + GameFileIndex.ShardPath(hash), request.Uri.AbsolutePath);
    }

    /// <summary>
    ///     A shard holds every file whose hash shares a first byte, so the answer is only the entries for the
    ///     exact hash asked about. Anything else would hand the restorer some other file to download.
    /// </summary>
    [Fact]
    public async Task OnlyTheExactHashIsTakenOutOfTheShard()
    {
        var wanted = Hash.FromHex("ab00000000000001");
        var neighbour = Hash.FromHex("ab00000000000002");
        _http.Respond = _ => Json(new[]
        {
            new IndexedGameFile {Hash = neighbour, App = 489830, Depot = 1, Manifest = 10, Path = "Data\\Other.esm"},
            new IndexedGameFile {Hash = wanted, App = 489830, Depot = 2, Manifest = 20, Path = "Data\\Skyrim.esm"},
            new IndexedGameFile {Hash = wanted, App = 489830, Depot = 3, Manifest = 30, Path = "Data\\Skyrim.esm"}
        });

        var found = await Index().Find(Game.SkyrimSpecialEdition, wanted, CancellationToken.None);

        Assert.Equal(new uint[] {2, 3}, Array.ConvertAll(found, f => f.Depot));
        Assert.All(found, f => Assert.Equal(wanted, f.Hash));
    }

    /// <summary>A shard nobody has written yet is a 404, and means "nothing indexed", not an error.</summary>
    [Fact]
    public async Task AShardNobodyWroteIsEmpty()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        Assert.Empty(await Index().Find(Game.SkyrimSpecialEdition, Hash.FromHex("cd00000000000000"),
            CancellationToken.None));
    }

    /// <summary>
    ///     A server error reading a shard is passed up. The restorer is what decides an unreadable content
    ///     index is a fall-through, and it can only do that if it is told.
    /// </summary>
    [Fact]
    public async Task AServerErrorReadingAShardIsNotHidden()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Index().Find(Game.SkyrimSpecialEdition, Hash.FromHex("cd00000000000000"), CancellationToken.None));
        Assert.Equal(HttpStatusCode.InternalServerError, thrown.StatusCode);
    }

    private sealed class ScriptedHttp : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        public List<(HttpMethod Method, Uri Uri)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add((request.Method, request.RequestUri!));
            return Task.FromResult(Respond(request));
        }
    }

    private sealed class NoKeys : ITokenProvider<WabbajackApiState>
    {
        public ValueTask<WabbajackApiState?> Get()
        {
            return ValueTask.FromResult<WabbajackApiState?>(new WabbajackApiState {MetricsKey = "test"});
        }

        public ValueTask SetToken(WabbajackApiState val)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> Delete()
        {
            return ValueTask.FromResult(false);
        }

        public bool HaveToken()
        {
            return true;
        }
    }
}
