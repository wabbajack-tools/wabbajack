using System;
using System.Linq;

namespace Wabbajack.Networking.WabbajackClientApi;

public static class GitHubRawUrl
{
    public static Uri Normalize(Uri url)
    {
        if (!url.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            return url;

        var segments = url.AbsolutePath.Split('/');
        if (segments.Length < 6)
            return url;
        if (!segments[3].Equals("refs", StringComparison.OrdinalIgnoreCase))
            return url;
        if (!segments[4].Equals("heads", StringComparison.OrdinalIgnoreCase) &&
            !segments[4].Equals("tags", StringComparison.OrdinalIgnoreCase))
            return url;

        var path = string.Join('/', segments.Take(3).Concat(segments.Skip(5)));
        return new Uri(url.GetLeftPart(UriPartial.Authority) + path + url.Query);
    }
}
