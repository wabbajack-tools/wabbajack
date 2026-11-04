using System;
using System.Collections.Generic;

namespace Wabbajack.Paths;

/// <summary>
///     Gives every spelling of a path one casing. Modlists are built on Windows, where <c>Textures\a.dds</c> and
///     <c>textures\b.dds</c> land in the same folder; on a case-sensitive file system they make two folders, and a
///     game reading through Wine finds only one of them. The first spelling seen of each folder or file is the one
///     every later spelling takes, one path component at a time, so a folder keeps its casing even when the paths
///     inside it disagree about it.
/// </summary>
public sealed class PathCasing
{
    // Keyed by the whole path up to and including a component, compared without case, holding that component's
    // chosen spelling.
    private readonly Dictionary<string, string> _chosen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The path in the casing chosen for it, choosing it for whatever has not been seen yet.</summary>
    public RelativePath Unify(RelativePath path)
    {
        var parts = path.Parts;
        if (parts == null || parts.Length == 0) return path;

        var unified = new string[parts.Length];
        var prefix = "";
        for (var i = 0; i < parts.Length; i++)
        {
            prefix = i == 0 ? parts[i] : prefix + "\\" + parts[i];
            if (!_chosen.TryGetValue(prefix, out var spelling))
            {
                spelling = parts[i];
                _chosen[prefix] = spelling;
            }

            unified[i] = spelling;
        }

        return RelativePath.FromParts(unified);
    }
}
