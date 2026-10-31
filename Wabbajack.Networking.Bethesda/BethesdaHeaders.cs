using System.Net.Http.Headers;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     The header set every call to <c>api.bethesda.net</c> carries, assembled exactly as the game does.
///     <para>
///         None of it is optional. Omitting <c>x-bnet-key</c> and sending a key the edge does not know are
///         answered differently, but both are refusals, and the rest is what the origin checks a request
///         against once the edge has let it through.
///     </para>
/// </summary>
public static class BethesdaHeaders
{
    /// <summary>
    ///     Header names whose values must never reach a log, a crash report or a bug attachment. Taken from
    ///     the SDK's own redaction list, which is wider than what this code sends, because the cost of
    ///     carrying a name that never appears is nothing and the cost of missing one is a shared secret or a
    ///     session token in a file a user is about to paste into a support channel.
    /// </summary>
    public static readonly IReadOnlySet<string> Secret = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "x-bnet-key",
        "x-session-token",
        "x-client-api-key",
        "x-server-api-key",
        "x-server-private-key",
        "authorization"
    };

    /// <summary>What a header's value may be written down as.</summary>
    public static string Redact(string name, string value)
    {
        return Secret.Contains(name) ? "<redacted>" : value;
    }

    /// <summary>The whole set, redacted, in a form safe to log.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Redacted(
        IEnumerable<KeyValuePair<string, string>> headers)
    {
        return headers.Select(h => new KeyValuePair<string, string>(h.Key, Redact(h.Key, h.Value))).ToArray();
    }

    /// <summary>
    ///     The headers for one request. <c>x-session-token</c> appears only once a session has been
    ///     established, which is what makes the first call - the login itself - the one request that carries
    ///     no session.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Build(string? sessionToken = null,
        string country = BethesdaConstants.DefaultCountry, string language = BethesdaConstants.DefaultLanguage)
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            new("x-bnet-agent", BethesdaConstants.Agent),
            new("x-bnet-key", BethesdaConstants.ApiKey),
            new("x-country", country),
            new("x-language", language),
            new("x-product", BethesdaConstants.Product),
            new("x-platform", BethesdaConstants.Platform),
            new("user-agent", BethesdaConstants.UserAgent)
        };

        if (!string.IsNullOrEmpty(sessionToken))
            headers.Add(new KeyValuePair<string, string>("x-session-token", sessionToken));

        return headers;
    }

    /// <summary>Puts <see cref="Build" />'s answer on a request.</summary>
    public static void Apply(HttpRequestMessage message, IEnumerable<KeyValuePair<string, string>> headers)
    {
        foreach (var (name, value) in headers)
            message.Headers.TryAddWithoutValidation(name, value);
    }

    /// <summary>
    ///     The headers a <c>.ckm</c> fetch carries. The download URLs are presigned and carry no auth of
    ///     their own, so the key and the session token are deliberately absent - there is no reason to send
    ///     a shared secret to a storage host that does not check it.
    /// </summary>
    public static void ApplyDownload(HttpRequestMessage message)
    {
        message.Headers.UserAgent.Add(new ProductInfoHeaderValue(BethesdaConstants.UserAgent, null));
    }
}
