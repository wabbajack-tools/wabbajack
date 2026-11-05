using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test.Offline;

/// <summary>
///     <see cref="BethesdaApiClient" /> against a scripted <c>api.bethesda.net</c>: what each call sends, and
///     what each kind of answer becomes. The messages matter as much as the outcomes, because they are what a
///     user sees when fetching a Creation fails, and each one points at a different fix - start Steam, update
///     Wabbajack, or wait for Bethesda.
/// </summary>
public class BethesdaApiClientHttpTests
{
    private const string LoginOk =
        """{"platform":{"code":2000,"message":"success","response":{"session_token":"sess-123","username":"dovahkiin"}}}""";

    private const string FulfilledOk = """{"platform":{"code":2000,"message":"success"}}""";

    private const string ArcaneArcherUrl =
        "https://ugcmods.bethesda.net/public/SKYRIM/client/5648/CSV2/CSV2.ckm?versionId=x";

    private static readonly string ResolveOk =
        """{"platform":{"code":2000,"response":{"data":[{"content_id":5648,"title":"Arcane Archer Pack","files":[{"download_url":"URL","checksum":"0123456789abcdef0123456789abcdef","size":10}]}]}}}"""
            .Replace("URL", ArcaneArcherUrl);

    private readonly ScriptedBethesda _http = new();
    private readonly FixedTicket _ticket = new(0xAB, 0xCD, 0x01);

    private BethesdaApiClient Client()
    {
        return new BethesdaApiClient(new HttpClient(_http), _ticket, NullLogger<BethesdaApiClient>.Instance);
    }

    private static JsonElement Body(SeenRequest request)
    {
        return JsonDocument.Parse(request.Body!).RootElement;
    }

    /// <summary>
    ///     The login carries the ticket as lower-case hex - base64 is refused with the same code a bad
    ///     ticket gets - and the edge key, and no session, because there is none yet.
    /// </summary>
    [Fact]
    public async Task SigningInSendsTheTicketAsHexWithTheKeyAndNoSession()
    {
        _http.ThenOk(LoginOk).ThenOk(FulfilledOk);

        await Client().SignIn();

        var login = _http.Requests[0];
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Equal("https://api.bethesda.net/session/external-login", login.Uri.ToString());
        Assert.Equal("application/json", login.ContentType);
        Assert.Equal("steam", Body(login).GetProperty("scheme").GetString());
        Assert.Equal("abcd01", Body(login).GetProperty("token").GetString());
        Assert.Equal("en", Body(login).GetProperty("language").GetString());
        Assert.Equal(BethesdaConstants.ApiKey, login.Headers["x-bnet-key"]);
        Assert.False(login.Headers.ContainsKey("x-session-token"));

        Assert.Equal(new[] {BethesdaConstants.SkyrimSpecialEditionAppId}, _ticket.AskedFor);
    }

    /// <summary>
    ///     The entitlement update reuses the same ticket and carries the session the login returned; without
    ///     it Bethesda does not know whose entitlements to update.
    /// </summary>
    [Fact]
    public async Task FulfillmentCarriesTheSessionAndTheSameTicket()
    {
        _http.ThenOk(LoginOk).ThenOk(FulfilledOk);
        var client = Client();

        await client.SignIn();

        var fulfillment = _http.Requests[1];
        Assert.Equal(HttpMethod.Post, fulfillment.Method);
        Assert.Equal("/vccs/fulfillment/update_first_party_entitlements", fulfillment.Uri.AbsolutePath);
        Assert.Equal("abcd01", Body(fulfillment).GetProperty("token").GetString());
        Assert.Equal("sess-123", fulfillment.Headers["x-session-token"]);
        Assert.Equal(2, _http.Requests.Count);

        Assert.True(client.HasSession);
        Assert.Equal("dovahkiin", client.Username);
    }

    /// <summary>
    ///     The app counts Bethesda.net sign-ins from this event, so it is raised once a sign-in has gone all
    ///     the way through, and not for one whose entitlement update failed.
    /// </summary>
    [Fact]
    public async Task SignedInIsRaisedOnlyWhenTheEntitlementsAreUpdated()
    {
        _http.ThenOk(LoginOk).ThenOk(FulfilledOk)
            .ThenOk(LoginOk).ThenOk("""{"platform":{"code":4000,"message":"entitlements unavailable"}}""");
        var client = Client();
        var raised = 0;
        client.SignedIn += () => raised++;

        await client.SignIn();
        Assert.Equal(1, raised);

        await Assert.ThrowsAsync<BethesdaApiException>(() => client.SignIn());
        Assert.Equal(1, raised);
    }

    /// <summary>A login Bethesda accepts without handing back a session is a failure, said as such.</summary>
    [Fact]
    public async Task ALoginWithNoSessionTokenFails()
    {
        _http.ThenOk("""{"platform":{"code":2000,"response":{"username":"dovahkiin"}}}""");
        var client = Client();

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(() => client.SignIn());

        Assert.Contains("returned no session token", thrown.Message);
        Assert.False(client.HasSession);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public async Task ALoginAnswerWithNoEnvelopeFails()
    {
        _http.ThenOk("""{"session_token":"sess-123"}""");

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(() => Client().SignIn());

        Assert.Contains("answered without a platform envelope", thrown.Message);
    }

    /// <summary>A 200 whose envelope carries no response is the origin saying no, and its code is kept.</summary>
    [Fact]
    public async Task ALoginEnvelopeWithNoResponseCarriesItsCode()
    {
        _http.ThenOk("""{"platform":{"code":3001,"message":"nope"}}""");

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(() => Client().SignIn());

        Assert.Equal(3001, thrown.PlatformCode);
        Assert.Contains("answered 3001: nope", thrown.Message);
    }

    /// <summary>
    ///     The entitlement update answers 2000 and nothing else on success. Anything other than 2000 is a
    ///     failure, even inside an HTTP 200.
    /// </summary>
    [Fact]
    public async Task AFulfillmentCodeOtherThanSuccessFails()
    {
        _http.ThenOk(LoginOk).ThenOk("""{"platform":{"code":4000,"message":"entitlements unavailable"}}""");

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(() => Client().SignIn());

        Assert.Equal(4000, thrown.PlatformCode);
        Assert.Contains("update_first_party_entitlements answered 4000", thrown.Message);
    }

    /// <summary>
    ///     After the entitlement update fails, the client should not claim a session: <c>CreationCache</c>
    ///     skips signing in when one is claimed, so the next file would resolve against entitlements that were
    ///     never updated, and the user would be told they "may not own" Creations they do own.
    /// </summary>
    [Fact]
    public async Task AFailedFulfillmentLeavesNoSessionBehind()
    {
        _http.ThenOk(LoginOk).ThenOk("""{"platform":{"code":4000,"message":"entitlements unavailable"}}""");
        var client = Client();

        await Assert.ThrowsAsync<BethesdaApiException>(() => client.SignIn());

        Assert.False(client.HasSession);
    }

    /// <summary>
    ///     Each refusal says what went wrong in terms a user or a maintainer can act on: an empty 403 is the
    ///     edge refusing the key this build ships, 5001 is a header problem, 14029 is the Steam ticket.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "", null, "was blocked before it reached Bethesda (403)")]
    [InlineData(HttpStatusCode.NotFound, """{"platform":{"code":5001,"message":"Invalid Header values"}}""", 5001,
        "was refused for its headers (404, 5001: Invalid Header values)")]
    [InlineData(HttpStatusCode.PreconditionFailed,
        """{"platform":{"code":14029,"message":"Error verifying authentication"}}""", 14029,
        "Steam has to be running and logged in on an account that owns Skyrim Special Edition")]
    [InlineData(HttpStatusCode.BadRequest, """{"platform":{"code":1234,"message":"Bad thing"}}""", 1234,
        "answered 400 (1234: Bad thing)")]
    public async Task ARefusalSaysWhatKindOfRefusalItWas(HttpStatusCode status, string body, int? code,
        string expected)
    {
        _http.Then(status, body);

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(() => Client().SignIn());

        Assert.Equal(status, thrown.Status);
        Assert.Equal(code, thrown.PlatformCode);
        Assert.Equal(code == null, thrown.BlockedAtEdge);
        Assert.Contains("POST /session/external-login", thrown.Message);
        Assert.Contains(expected, thrown.Message);
    }

    /// <summary>A 4xx is an answer, not a hiccup: asking again would get the same one and waste the user's time.</summary>
    [Fact]
    public async Task AClientErrorIsNotRetried()
    {
        _http.Then(HttpStatusCode.BadRequest, """{"platform":{"code":1234,"message":"Bad thing"}}""");

        await Assert.ThrowsAsync<BethesdaApiException>(() => Client().SignIn());

        Assert.Single(_http.Requests);
    }

    /// <summary>
    ///     A 503 is worth one more try, and a request that succeeds on the retry is a success. This waits on
    ///     the client's real one second back-off, so it is the only retry test here.
    /// </summary>
    [Fact]
    public async Task AServiceUnavailableAnswerIsRetried()
    {
        _http.Then(HttpStatusCode.ServiceUnavailable).ThenOk(LoginOk).ThenOk(FulfilledOk);
        var client = Client();

        await client.SignIn();

        Assert.Equal(3, _http.Requests.Count);
        Assert.All(_http.Requests.Take(2), r => Assert.Equal("/session/external-login", r.Uri.AbsolutePath));
        Assert.True(client.HasSession);
    }

    /// <summary>A 200 that is not JSON - an HTML error page from a proxy, say - is said plainly.</summary>
    [Fact]
    public async Task ASuccessThatIsNotJsonIsSaidPlainly()
    {
        _http.ThenOk("<html>maintenance</html>");

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(() => Client().SignIn());

        Assert.Contains("answered 200 with a body that is not JSON", thrown.Message);
    }

    /// <summary>The resolve sends the session and the matrix-parameter query, and hands back one slot per Creation.</summary>
    [Fact]
    public async Task ResolvingSendsTheSessionAndReadsTheSlots()
    {
        _http.ThenOk(LoginOk).ThenOk(FulfilledOk).ThenOk(ResolveOk);
        var client = Client();
        await client.SignIn();

        var slots = await client.Resolve(new long[] {5648, 5648});

        var resolve = _http.Requests[2];
        Assert.Equal(HttpMethod.Get, resolve.Method);
        Assert.Equal(BethesdaApiClient.ContentQuery(new long[] {5648}), resolve.Uri);
        Assert.Equal("sess-123", resolve.Headers["x-session-token"]);
        Assert.Null(resolve.Body);

        var slot = Assert.Single(slots);
        Assert.Equal(5648, slot.ContentId);
        Assert.Equal("Arcane Archer Pack", slot.Title);
        Assert.Equal(ArcaneArcherUrl, slot.Url.ToString());
        Assert.True(slot.ChecksumIsMd5);
    }

    [Fact]
    public async Task AResolveAnswerWithNoDataFails()
    {
        _http.ThenOk(LoginOk).ThenOk(FulfilledOk).ThenOk("""{"platform":{"code":2000,"response":{}}}""");
        var client = Client();
        await client.SignIn();

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(() => client.Resolve(new long[] {5648}));

        Assert.Contains("carried no data array", thrown.Message);
    }
}
