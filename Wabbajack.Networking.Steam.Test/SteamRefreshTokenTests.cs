using System;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

public class SteamRefreshTokenTests
{
    [Fact]
    public void ReadsTheExpClaimOfAWellFormedToken()
    {
        var expiry = DateTimeOffset.FromUnixTimeSeconds(1_760_000_000);
        var token = MakeJwt($"{{\"iss\":\"steam\",\"exp\":{expiry.ToUnixTimeSeconds()},\"sub\":\"1234\"}}");

        Assert.Equal(expiry, SteamRefreshToken.GetExpiry(token));
    }

    [Theory]
    // Not a JWT at all.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    // Wrong number of segments.
    [InlineData("only.two")]
    [InlineData("a.b.c.d")]
    // Payload is not decodable base64url: a length of 1 mod 4 can never be valid.
    [InlineData("aGVhZGVy.a.c2ln")]
    // Payload is not base64 at all.
    [InlineData("aGVhZGVy.!!!!.c2ln")]
    public void RefusesAMalformedTokenWithoutThrowing(string token)
    {
        Assert.Null(SteamRefreshToken.GetExpiry(token));
    }

    [Fact]
    public void RefusesAPayloadThatIsNotJson()
    {
        Assert.Null(SteamRefreshToken.GetExpiry(MakeJwt("this is not json")));
    }

    [Fact]
    public void RefusesAPayloadThatIsJsonButNotAnObject()
    {
        Assert.Null(SteamRefreshToken.GetExpiry(MakeJwt("[1, 2, 3]")));
    }

    [Fact]
    public void RefusesAPayloadWithNoExpClaim()
    {
        Assert.Null(SteamRefreshToken.GetExpiry(MakeJwt("{\"iss\":\"steam\"}")));
    }

    [Fact]
    public void RefusesAnExpThatIsNotANumber()
    {
        Assert.Null(SteamRefreshToken.GetExpiry(MakeJwt("{\"exp\":\"soon\"}")));
    }

    [Fact]
    public void RefusesAnExpOutsideTheRangeOfADate()
    {
        Assert.Null(SteamRefreshToken.GetExpiry(MakeJwt("{\"exp\":9223372036854775807}")));
    }

    [Fact]
    public void DecodesAPayloadThatUsesTheUrlSafeAlphabet()
    {
        // Real Steam tokens are base64url, not base64: '-' and '_' stand in for '+' and '/'. Feeding one
        // straight to Convert.FromBase64String either throws or decodes to different bytes, so find a
        // payload that actually lands on those characters and check it survives.
        var expiry = DateTimeOffset.FromUnixTimeSeconds(1_760_000_001);

        string? urlSafePayload = null;
        for (var i = 0; i < 256 && urlSafePayload == null; i++)
        {
            var candidate = $"{{\"exp\":{expiry.ToUnixTimeSeconds()},\"pad\":\"{new string('~', i)}\"}}";
            var standard = Convert.ToBase64String(Encoding.UTF8.GetBytes(candidate));
            if (standard.Contains('+') || standard.Contains('/')) urlSafePayload = candidate;
        }

        Assert.NotNull(urlSafePayload);

        var token = MakeJwt(urlSafePayload!);
        var encoded = token.Split('.')[1];
        Assert.True(encoded.Contains('-') || encoded.Contains('_'),
            "the encoded payload should exercise the url-safe alphabet");

        Assert.Equal(expiry, SteamRefreshToken.GetExpiry(token));
    }

    [Fact]
    public void AnUnknownExpiryIsNeverTreatedAsExpired()
    {
        Assert.False(SteamRefreshToken.IsExpired(null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AnExpiryInThePastIsExpired()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(SteamRefreshToken.IsExpired(now - TimeSpan.FromDays(1), now));
    }

    [Fact]
    public void AnExpiryWithinTheGracePeriodIsAlreadyExpired()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(SteamRefreshToken.IsExpired(now + SteamRefreshToken.ExpiryGracePeriod - TimeSpan.FromSeconds(1), now));
    }

    [Fact]
    public void AnExpiryBeyondTheGracePeriodIsNotExpired()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(SteamRefreshToken.IsExpired(now + SteamRefreshToken.ExpiryGracePeriod + TimeSpan.FromSeconds(1), now));
    }

    [Fact]
    public void RoundTripsAnExpiryThroughTheDecoder()
    {
        // The property that matters: whatever exp a token carries comes back out unchanged.
        foreach (var seconds in new long[] {0, 1, 1_700_000_000, 4_102_444_800})
        {
            var token = MakeJwt(JsonSerializer.Serialize(new {exp = seconds}));
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(seconds), SteamRefreshToken.GetExpiry(token));
        }
    }

    private static string MakeJwt(string payload)
    {
        return $"{Base64Url("{\"typ\":\"JWT\"}")}.{Base64Url(payload)}.{Base64Url("signature")}";
    }

    private static string Base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
