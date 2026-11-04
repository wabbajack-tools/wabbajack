#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack.Networking.NexusApi.OAuth;

/// <summary>
///     Building a query string and reading one back. Both halves of the login turn on getting these exactly
///     right - the authorize URL carries the redirect URI out, the redirect carries the code back - so they
///     are pure functions that can be checked without a socket or a browser.
/// </summary>
public static class OAuthQuery
{
    /// <summary>
    ///     <paramref name="url" /> with <paramref name="parameters" /> appended as a query string. The URL is
    ///     assumed to carry none of its own, which is true of every endpoint here.
    /// </summary>
    public static Uri AddTo(string url, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var query = string.Join("&",
            parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return query.Length == 0 ? new Uri(url) : new Uri($"{url}?{query}");
    }

    /// <summary>
    ///     A URL query string as a dictionary, with or without its leading '?'.
    ///     <para>
    ///         A key that appears twice keeps its first value. A duplicate is malformed either way - RFC 6749
    ///         says a request parameter must not be sent more than once - and the first one is the one the
    ///         authorization server put there, so anything appended after it loses.
    ///     </para>
    ///     <para>
    ///         '+' decodes to a space, which is what browsers and every other query parser do.
    ///     </para>
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string? query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query)) return parsed;

        foreach (var pair in query.TrimStart('?').Split('&'))
        {
            if (pair.Length == 0) continue;

            var split = pair.IndexOf('=');
            var key = Decode(split < 0 ? pair : pair[..split]);
            var value = split < 0 ? string.Empty : Decode(pair[(split + 1)..]);
            parsed.TryAdd(key, value);
        }

        return parsed;
    }

    private static string Decode(string value)
    {
        return Uri.UnescapeDataString(value.Replace('+', ' '));
    }
}
