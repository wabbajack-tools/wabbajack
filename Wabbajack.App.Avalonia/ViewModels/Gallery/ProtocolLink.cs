using System;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

/// <summary>
///     What a <c>wabbajack://</c> link asks for. The website and Nexus Mods send these, and browsers are not kind
///     to them on the way: a link can arrive percent-escaped, or with the colon dropped from <c>https://</c>, so
///     the repair has to happen before the link is classified, not after.
/// </summary>
public abstract record ProtocolLink
{
    /// <summary>A Nexus Mods collection: <c>nexus-collection/{slug}</c>, or a revision of one.</summary>
    public sealed record NexusCollection(string Slug, int? Revision) : ProtocolLink;

    /// <summary>A .wabbajack file to download, and the name to save it under.</summary>
    public sealed record DirectDownload(Uri Url, string FileName) : ProtocolLink;

    /// <summary>A gallery list, by <c>repository/machineUrl</c> or by its machine URL alone.</summary>
    public sealed record GalleryList(string Id, bool Namespaced) : ProtocolLink;

    public sealed record Invalid(string Reason) : ProtocolLink;

    public static ProtocolLink Parse(string? payload)
    {
        var normalized = (payload ?? string.Empty).Trim().Trim('/');
        if (normalized.Length == 0)
            return new Invalid("The link names no modlist");

        if (normalized.StartsWith("nexus-collection/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = normalized.Split('/');
            if (parts[1].Length == 0)
                return new Invalid($"Invalid Nexus Collection URL format: {normalized}");
            int? revision = parts.Length >= 4 && parts[2].Equals("revision", StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(parts[3], out var number)
                ? number
                : null;
            return new NexusCollection(parts[1], revision);
        }

        var url = RepairUrl(normalized);
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".wabbajack", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
                return new Invalid($"Not a download link: {url}");
            return new DirectDownload(uri, FileNameFor(uri));
        }

        return new GalleryList(normalized, normalized.Contains('/'));
    }

    /// <summary>Undoes what a browser does to a URL inside a custom-scheme link.</summary>
    public static string RepairUrl(string link)
    {
        var url = Uri.UnescapeDataString(link);
        if (url.StartsWith("http//", StringComparison.OrdinalIgnoreCase))
            url = "http://" + url["http//".Length..];
        else if (url.StartsWith("https//", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url["https//".Length..];
        return url;
    }

    /// <summary>The URL's own file name, made into a .wabbajack file if it is not one.</summary>
    public static string FileNameFor(Uri url)
    {
        var name = System.IO.Path.GetFileName(url.LocalPath);
        if (string.IsNullOrWhiteSpace(name)) name = "modlist";
        return name.EndsWith(".wabbajack", StringComparison.OrdinalIgnoreCase) ? name : name + ".wabbajack";
    }
}
