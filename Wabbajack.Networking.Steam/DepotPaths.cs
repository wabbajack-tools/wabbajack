using SteamKit2;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Matching a wanted file against the paths a depot manifest carries.
///     The two sides are written by different people for different reasons: a modlist records a path
///     relative to the game folder, while a manifest records whatever Valve put in the depot, which for a
///     Windows game means backslashes and whatever casing the build happened to use. So the comparison has
///     to be forgiving, but only in ways that cannot pick the wrong file.
///     The rule, in order:
///     <list type="number">
///         <item>
///             Normalise both sides -- forward slashes become backslashes, leading and trailing separators
///             go, and a leading <c>.\</c> goes -- then compare the whole path ordinal-ignore-case.
///         </item>
///         <item>
///             Failing that, accept a manifest path that <em>ends with</em> the wanted path at a separator
///             boundary, which is what a depot rooted a level above the game folder looks like. This is only
///             accepted when exactly one file in the manifest matches that way; two candidates mean the
///             caller has not said enough to identify a file, and guessing between them is how the wrong
///             bytes end up on someone's disk.
///         </item>
///     </list>
///     Matching on the file name alone is deliberately not a step. Depots are full of repeated names.
/// </summary>
public static class DepotPaths
{
    /// <summary>
    ///     Puts a path into the one form both sides are compared in. Not a security boundary -- it does not
    ///     resolve <c>..</c> -- it just removes the differences that are only ever spelling.
    /// </summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var normalized = path.Trim().Replace('/', '\\');

        while (normalized.StartsWith(".\\", StringComparison.Ordinal))
            normalized = normalized[2..];

        return normalized.Trim('\\');
    }

    /// <summary>True when two paths name the same file under the rule above, ignoring the suffix step.</summary>
    public static bool AreSame(string a, string b)
    {
        return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     True when <paramref name="manifestPath" /> ends with <paramref name="wanted" /> on a separator
    ///     boundary. <c>Data\Skyrim.esm</c> matches <c>SkyrimSE\Data\Skyrim.esm</c> but not
    ///     <c>Data\NotSkyrim.esm</c>.
    /// </summary>
    public static bool EndsWithPath(string manifestPath, string wanted)
    {
        var haystack = Normalize(manifestPath);
        var needle = Normalize(wanted);

        if (needle.Length == 0) return false;
        if (haystack.Length == needle.Length)
            return string.Equals(haystack, needle, StringComparison.OrdinalIgnoreCase);
        if (haystack.Length < needle.Length) return false;

        return haystack[haystack.Length - needle.Length - 1] == '\\' &&
               haystack.EndsWith(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Finds the one file in a manifest that <paramref name="wanted" /> names, or null.
    ///     Directory entries are skipped: they carry a path and no content, so a caller asking for
    ///     <c>Data</c> should be told nothing was found rather than handed an empty file.
    /// </summary>
    public static DepotManifest.FileData? Find(IEnumerable<DepotManifest.FileData> files, string wanted)
    {
        var candidates = files.Where(f => !f.Flags.HasFlag(EDepotFileFlag.Directory)).ToArray();

        // Two entries whose normalised paths are equal can only differ in case, and no Windows install can
        // hold both at once, so the first is as good an answer as any.
        var exact = candidates.FirstOrDefault(f => AreSame(f.FileName, wanted));
        if (exact != null) return exact;

        var suffix = candidates.Where(f => EndsWithPath(f.FileName, wanted)).ToArray();
        return suffix.Length == 1 ? suffix[0] : null;
    }
}
