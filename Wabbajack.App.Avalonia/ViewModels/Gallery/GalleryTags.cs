using System.Collections.Generic;
using System.Linq;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

/// <summary>The tags a gallery tile shows.</summary>
public static class GalleryTags
{
    /// <summary>
    ///     A list's own tags, each mapped through <c>tag_mappings.json</c> ("Requiem" is shown as "Hardcore") and
    ///     kept only if the result is an allowed tag, in its allowed spelling; then Unavailable, Featured and NSFW,
    ///     which the gallery adds itself. The mapping used to be looked up and then thrown away, so a list tagged
    ///     with a mapped name lost the tag.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string> raw, HashSet<string> allowed,
        IReadOnlyDictionary<string, string> mappings, bool nsfw, bool official, bool unavailable)
    {
        var tags = new List<string>();
        foreach (var tag in raw)
        {
            var name = mappings.TryGetValue(tag, out var mapped) ? mapped : tag;
            if (allowed.TryGetValue(name, out var spelled) && !tags.Contains(spelled))
                tags.Add(spelled);
        }

        if (nsfw) tags.Insert(0, "NSFW");
        if (official) tags.Insert(0, "Featured");
        if (unavailable) tags.Insert(0, "Unavailable");
        return tags;
    }
}
