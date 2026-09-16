using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The client, and the one live check worth keeping: that the key this build ships is still the one the
///     edge and the origin recognise.
/// </summary>
public class BethesdaApiClientTests
{
    /// <summary>
    ///     Reading the origin's envelope out of a refusal. The distinction the rest of the client leans on
    ///     is between a body that has one and a body that does not: no envelope means the request never
    ///     reached Bethesda, which is a different problem with a different fix.
    /// </summary>
    [Theory]
    [InlineData("""{"platform":{"code":14029,"message":"Error verifying authentication"}}""", 14029,
        "Error verifying authentication")]
    [InlineData("""{"platform":{"code":5001,"message":"Invalid Header values"}}""", 5001, "Invalid Header values")]
    [InlineData("""{"platform":{"code":2000,"response":{}}}""", 2000, null)]
    public void ReadsAPlatformErrorOutOfABody(string body, int code, string? message)
    {
        var (actualCode, actualMessage) = BethesdaApiClient.PlatformError(body);

        Assert.Equal(code, actualCode);
        Assert.Equal(message, actualMessage);
    }

    /// <summary>
    ///     An empty body, an HTML error page and a JSON body with no envelope all mean the same thing: the
    ///     answer is not Bethesda's.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><head><title>403 Forbidden</title></head></html>")]
    [InlineData("""{"message":"Forbidden"}""")]
    [InlineData("""{"platform":"not an object"}""")]
    public void SaysNothingReachedTheOriginWhenNothingDid(string body)
    {
        var (code, message) = BethesdaApiClient.PlatformError(body);

        Assert.Null(code);
        Assert.Null(message);
    }

    [Fact]
    public async Task RefusesToResolveWithoutASession()
    {
        var client = new BethesdaApiClient(new HttpClient(), new NoTicket(),
            NullLogger<BethesdaApiClient>.Instance);

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(async () =>
            await client.Resolve(new long[] {5648}));

        Assert.Equal(HttpStatusCode.Unauthorized, thrown.Status);
    }

    /// <summary>Asking about nothing costs nothing, and in particular costs no request.</summary>
    [Fact]
    public async Task AsksNothingWhenThereIsNothingToAskAbout()
    {
        var client = new BethesdaApiClient(new HttpClient(), new NoTicket(),
            NullLogger<BethesdaApiClient>.Instance);

        Assert.Empty(await client.Resolve(Array.Empty<long>()));
    }

    [Fact]
    public async Task SaysSoWhenSteamHandsOverNoTicket()
    {
        var client = new BethesdaApiClient(new HttpClient(), new NoTicket(),
            NullLogger<BethesdaApiClient>.Instance);

        var thrown = await Assert.ThrowsAsync<BethesdaApiException>(async () => await client.SignIn());

        Assert.Contains("empty encrypted app ticket", thrown.Message);
    }

    /// <summary>
    ///     The probe that settled which of the four candidate keys recovered from the game binary is the
    ///     production one, re-run against the live edge. It needs no credential: the ticket is deliberately
    ///     garbage, and the three answers are told apart by how far into Bethesda's stack the request got.
    ///     <list type="bullet">
    ///         <item>an unrecognised key never reaches the origin - 403 with an empty body, from CloudFront;</item>
    ///         <item>a key for another Bethesda environment reaches the origin and is refused there - 404, platform code 5001, the same answer as sending no key at all;</item>
    ///         <item>the production key reaches the ticket check - 412, platform code 14029, which is the garbage ticket failing and therefore the key passing.</item>
    ///     </list>
    ///     A failure here means the shipped key has been rotated or revoked, which is the one thing about
    ///     this chain that can break without anything in the repo changing.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public async Task OnlyTheProductionKeyReachesTicketVerification()
    {
        using var http = new HttpClient();
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri(BethesdaConstants.ApiBase, "/session/external-login"));
        BethesdaHeaders.Apply(message, BethesdaHeaders.Build());
        message.Content = new StringContent("""{"scheme":"steam","token":"00000000","language":"en"}""",
            Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(message);
        var body = await response.Content.ReadAsStringAsync();
        var (code, _) = BethesdaApiClient.PlatformError(body);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(14029, code);
    }

    /// <summary>A source that has nothing to give, for the paths that must not reach the network at all.</summary>
    private sealed class NoTicket : ISteamAppTicketSource
    {
        public ValueTask<byte[]> GetEncryptedAppTicket(uint appId, CancellationToken token = default)
        {
            return ValueTask.FromResult(Array.Empty<byte>());
        }
    }
}
