using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The parts of a request that are decided before anything is sent: the header set, how a ticket is
///     written down, and the resolve URL. All three were got wrong at least once while the protocol was
///     being worked out, and every one of those mistakes came back as an authentication error that said
///     nothing about the real cause.
/// </summary>
public class BethesdaRequestTests
{
    private static Dictionary<string, string> Headers(string? sessionToken = null)
    {
        return BethesdaHeaders.Build(sessionToken).ToDictionary(h => h.Key, h => h.Value);
    }

    [Fact]
    public void SendsTheHeaderSetTheGameSends()
    {
        var headers = Headers();

        Assert.Equal(BethesdaConstants.ApiKey, headers["x-bnet-key"]);
        Assert.Equal("1.6.1170.0", headers["x-bnet-agent"]);
        Assert.Equal("SKYRIM", headers["x-product"]);
        Assert.Equal("STEAM", headers["x-platform"]);
        Assert.Equal("US", headers["x-country"]);
        Assert.Equal("en", headers["x-language"]);
        Assert.Equal("bnet", headers["user-agent"]);
    }

    /// <summary>
    ///     The login is the one call with no session to send, and sending an empty one would be a header the
    ///     origin has to decide about rather than one it never sees.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CarriesNoSessionHeaderUntilThereIsASession(string? sessionToken)
    {
        Assert.DoesNotContain("x-session-token", Headers(sessionToken).Keys);
    }

    [Fact]
    public void CarriesTheSessionOnceThereIsOne()
    {
        Assert.Equal("a-session-token", Headers("a-session-token")["x-session-token"]);
    }

    /// <summary>
    ///     The key and the session token are the two things here that must never be written down. The list
    ///     is wider than what is sent, which costs nothing and covers a header added later.
    /// </summary>
    [Theory]
    [InlineData("x-bnet-key")]
    [InlineData("X-BNET-KEY")]
    [InlineData("x-session-token")]
    [InlineData("authorization")]
    [InlineData("x-client-api-key")]
    [InlineData("x-server-api-key")]
    [InlineData("x-server-private-key")]
    public void NeverWritesDownASecret(string name)
    {
        Assert.Equal("<redacted>", BethesdaHeaders.Redact(name, "the actual secret"));
    }

    [Theory]
    [InlineData("x-bnet-agent")]
    [InlineData("x-product")]
    [InlineData("user-agent")]
    public void WritesDownEverythingElseAsItIs(string name)
    {
        Assert.Equal("a value", BethesdaHeaders.Redact(name, "a value"));
    }

    [Fact]
    public void RedactsAWholeHeaderSetWithoutLosingIt()
    {
        var redacted = BethesdaHeaders.Redacted(BethesdaHeaders.Build("a-session-token"))
            .ToDictionary(h => h.Key, h => h.Value);

        Assert.Equal(BethesdaHeaders.Build("a-session-token").Count, redacted.Count);
        Assert.Equal("<redacted>", redacted["x-bnet-key"]);
        Assert.Equal("<redacted>", redacted["x-session-token"]);
        Assert.Equal("SKYRIM", redacted["x-product"]);
        Assert.DoesNotContain(BethesdaConstants.ApiKey, string.Join(";", redacted.Values));
    }

    [Fact]
    public void PutsTheHeadersOnTheRequestItself()
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "https://api.bethesda.net/");
        BethesdaHeaders.Apply(message, BethesdaHeaders.Build("a-session-token"));

        Assert.Equal("SKYRIM", Assert.Single(message.Headers.GetValues("x-product")));
        Assert.Equal("a-session-token", Assert.Single(message.Headers.GetValues("x-session-token")));
    }

    /// <summary>
    ///     A <c>.ckm</c> URL is presigned and checks nothing, so it gets the user agent and none of the rest.
    ///     Sending a shared secret to a host that does not want it is a habit worth not having.
    /// </summary>
    [Fact]
    public void SendsNoSecretToTheDownloadHost()
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "https://ugcmods.bethesda.net/x.ckm");
        BethesdaHeaders.ApplyDownload(message);

        Assert.Equal("bnet", message.Headers.UserAgent.ToString());
        Assert.False(message.Headers.Contains("x-bnet-key"));
        Assert.False(message.Headers.Contains("x-session-token"));
    }

    /// <summary>
    ///     Hex, not base64. Base64 is refused with the same 412/14029 a garbage ticket gets, so getting this
    ///     wrong looks exactly like a ticket problem and sends you looking at Steam.
    /// </summary>
    [Fact]
    public void WritesATicketAsHex()
    {
        var ticket = new byte[] {0x00, 0x0f, 0x10, 0xff, 0xab};

        Assert.Equal("000f10ffab", BethesdaApiClient.ToHex(ticket));
        Assert.NotEqual(Convert.ToBase64String(ticket), BethesdaApiClient.ToHex(ticket));
    }

    [Fact]
    public void WritesAWholeTicketAndNothingElse()
    {
        var ticket = new byte[159];
        Random.Shared.NextBytes(ticket);

        var hex = BethesdaApiClient.ToHex(ticket);

        Assert.Equal(ticket.Length * 2, hex.Length);
        Assert.Equal(ticket, Convert.FromHexString(hex));
        Assert.Equal(hex.ToLowerInvariant(), hex);
    }

    /// <summary>
    ///     Matrix parameters, and <c>content/v1/content</c>. The <c>&amp;</c>-separated form and the shorter
    ///     path both reach a real endpoint that answers 400 for these ids, which is the kind of wrong answer
    ///     that reads as "the ids are bad".
    /// </summary>
    [Fact]
    public void BuildsTheResolveUrlWithMatrixParameters()
    {
        var uri = BethesdaApiClient.ContentQuery(new long[] {5615, 5617, 5618});

        Assert.Equal("https://api.bethesda.net/ugcmods/content/v1/content", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("?product=SKYRIM;content_ids=5615,5617,5618;download=true;size=8", uri.Query);
        Assert.DoesNotContain("&", uri.Query);
    }

    /// <summary>
    ///     <c>size</c> is the page size, so a default that did not exceed the id count would silently return
    ///     a short answer and look like Creations the account does not own.
    /// </summary>
    [Fact]
    public void AsksForAPageBigEnoughToHoldTheAnswer()
    {
        var ids = Enumerable.Range(5615, 74).Select(i => (long) i).ToArray();

        var uri = BethesdaApiClient.ContentQuery(ids);

        Assert.Contains("size=79", uri.Query);
    }

    [Fact]
    public void TakesAPageSizeWhenGivenOne()
    {
        Assert.Contains("size=250", BethesdaApiClient.ContentQuery(new long[] {5615}, 250).Query);
    }
}
