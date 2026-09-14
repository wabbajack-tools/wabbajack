using System.Text.Json;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Steam's refresh tokens are JWTs. How long one lasts is not documented in any primary source, so the
///     token's own <c>exp</c> claim is the only expiry signal available: decode it once when the token is
///     stored and treat that as the truth.
/// </summary>
public static class SteamRefreshToken
{
    /// <summary>
    ///     How far ahead of the stated expiry a token is treated as already dead, so a login does not start
    ///     against a token that will lapse mid-flow.
    /// </summary>
    public static readonly TimeSpan ExpiryGracePeriod = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Decodes the <c>exp</c> claim of a JWT. Returns null for anything that is not a decodable JWT with a
    ///     sane <c>exp</c>; a null means "unknown", never "expired".
    /// </summary>
    public static DateTimeOffset? GetExpiry(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var segments = token.Split('.');
        if (segments.Length != 3) return null;

        if (!TryBase64UrlDecode(segments[1], out var payload)) return null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("exp", out var exp)) return null;
            if (exp.ValueKind != JsonValueKind.Number) return null;
            if (!exp.TryGetInt64(out var seconds)) return null;
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            // An exp outside the range DateTimeOffset can hold. Nothing useful to say about it.
            return null;
        }
    }

    /// <summary>
    ///     True when a stored token is past its stated expiry, allowing for <see cref="ExpiryGracePeriod" />.
    ///     An unknown expiry is not expired -- the only way to find out is to try it.
    /// </summary>
    public static bool IsExpired(DateTimeOffset? expiry, DateTimeOffset now)
    {
        return expiry.HasValue && expiry.Value - ExpiryGracePeriod <= now;
    }

    private static bool TryBase64UrlDecode(string segment, out byte[] decoded)
    {
        decoded = Array.Empty<byte>();

        var padded = segment.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 0:
                break;
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            default:
                // A length of 1 mod 4 cannot be valid base64.
                return false;
        }

        try
        {
            decoded = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
