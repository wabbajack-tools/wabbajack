using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     Which of a modlist's archives come from a host on the ban list. A ban names a host, and covers that host
///     and every subdomain of it, so <c>modholdmods.com</c> also bans <c>www.modholdmods.com</c> and
///     <c>cdn.modholdmods.com</c> but not <c>notmodholdmods.com</c>.
/// </summary>
public static class BannedSources
{
    /// <summary>
    ///     The list as hosts: lower case, no scheme or path, no leading <c>*.</c> or <c>www.</c>, so whoever edits
    ///     Bans.yml can write a URL or a wildcard and still mean the host.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string?>? entries)
    {
        if (entries == null) return Array.Empty<string>();
        var hosts = new List<string>();
        foreach (var entry in entries)
        {
            var host = entry?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(host)) continue;
            if (host.Contains("://") && Uri.TryCreate(host, UriKind.Absolute, out var uri))
                host = uri.Host;
            host = host.Split('/')[0].Trim('.');
            if (host.StartsWith("*.")) host = host[2..];
            if (host.StartsWith("www.")) host = host[4..];
            if (host.Length > 0 && !hosts.Contains(host)) hosts.Add(host);
        }

        return hosts;
    }

    /// <summary>Whether <paramref name="host" /> is the banned host or a subdomain of it.</summary>
    public static bool Matches(string host, string ban)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        return host == ban || host.EndsWith("." + ban, StringComparison.Ordinal);
    }

    /// <summary>The ban <paramref name="state" /> falls under, or null when it falls under none.</summary>
    public static string? BanFor(IDownloadState state, IReadOnlyList<string> bans)
    {
        if (bans.Count == 0) return null;
        foreach (var url in UrlsOf(state))
        {
            if (!url.IsAbsoluteUri || string.IsNullOrEmpty(url.Host)) continue;
            var ban = bans.FirstOrDefault(b => Matches(url.Host, b));
            if (ban != null) return ban;
        }

        return null;
    }

    /// <summary>
    ///     Every address the state would send a download or a browser to: its own URL where it has one, and the
    ///     page a manual download opens, which for a WabbajackCDN file is not the same host.
    /// </summary>
    private static IEnumerable<Uri> UrlsOf(IDownloadState state)
    {
        var own = state switch
        {
            Http http => http.Url,
            Manual manual => manual.Url,
            MediaFire mediaFire => mediaFire.Url,
            Mega mega => mega.Url,
            ModDB modDb => modDb.Url,
            WabbajackCDN cdn => cdn.Url,
            _ => null
        };
        if (own != null) yield return own;
        if (ManualDownloadUrls.TryGet(state, out var target) && target.Url != own) yield return target.Url;
    }
}
