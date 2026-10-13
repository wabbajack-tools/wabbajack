using System;

namespace Wabbajack.App.Avalonia.ViewModels.Compiler;

/// <summary>The decisions around publishing a list to Nexus Mods as a collection, kept out of the view model.</summary>
public static class CollectionPublishing
{
    /// <summary>
    ///     The collection a publish should update: the one stored for this list, unless it belongs to another
    ///     game, in which case it is ignored and a new collection is made, rather than another game's collection
    ///     being overwritten.
    /// </summary>
    public static (string? CollectionId, string? Slug) UsableMapping(string? collectionId, string? slug,
        string? storedDomain, string listDomain)
    {
        if (string.IsNullOrWhiteSpace(collectionId)) return (null, null);
        if (!string.IsNullOrWhiteSpace(storedDomain) &&
            !storedDomain.Equals(listDomain, StringComparison.OrdinalIgnoreCase))
            return (null, null);
        return (collectionId, slug);
    }

    /// <summary>
    ///     What the publish panel says about the collection already on Nexus: its latest published revision, or
    ///     while it is still a draft, the revision this list last stored, marked as a draft.
    /// </summary>
    public static (int? Revision, bool IsDraft) ExistingRevision(int? latestPublished, int? lastStored)
    {
        return latestPublished.HasValue ? (latestPublished, false) : (lastStored, lastStored.HasValue);
    }

    /// <summary>
    ///     The game version a collection asks for, from the game executable's version info: ProductVersion, or
    ///     FileVersion when that says nothing. Placeholder versions some executables carry are not versions.
    /// </summary>
    public static string? GameVersion(string? productVersion, string? fileVersion)
    {
        var pv = productVersion?.Trim();
        var fv = fileVersion?.Trim();
        return IsMeaningful(pv) ? pv : IsMeaningful(fv) ? fv : null;
    }

    private static bool IsMeaningful(string? v) =>
        !string.IsNullOrWhiteSpace(v) &&
        v != "0.0.0.0" &&
        v != "1.0.0.0" &&
        v != "0, 0, 0, 0" &&
        v != "1, 0, 0, 0" &&
        !v.StartsWith("0.0.0") &&
        !v.StartsWith("1.0.0.0");

    public static string RevisionPage(string domain, string slug, int revision) =>
        $"https://www.nexusmods.com/games/{domain}/collections/{slug}/revisions/{revision}";
}
