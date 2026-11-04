using System;
using System.IO;
using System.Linq;

namespace Wabbajack.Paths.IO;

/// <summary>
///     Finds a file or folder the way Windows would. Modlists, MO2's profiles and the game index name files as they
///     were spelled on a Windows machine, and on a case-sensitive file system <c>Data\Textures</c> is not
///     <c>data\textures</c>. Where the exact spelling is missing, each component is matched against what is on disk
///     without case.
/// </summary>
public static class CaseInsensitiveLookup
{
    /// <summary>
    ///     <paramref name="relative" /> under <paramref name="root" />, spelled as it is on disk where a match exists;
    ///     the components with no match keep the spelling they were given. On Windows this is just
    ///     <c>root.Combine(relative)</c>.
    /// </summary>
    public static AbsolutePath FindIgnoringCase(this AbsolutePath root, RelativePath relative)
    {
        var exact = root.Combine(relative);
        if (OperatingSystem.IsWindows() || exact.FileExists() || exact.DirectoryExists()) return exact;

        var current = root;
        var parts = relative.Parts ?? Array.Empty<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            var next = current.Combine(parts[i]);
            if (!next.FileExists() && !next.DirectoryExists() && current.DirectoryExists())
            {
                var match = Directory.EnumerateFileSystemEntries(current.ToString())
                    .Select(Path.GetFileName)
                    .FirstOrDefault(name => string.Equals(name, parts[i], StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return parts.Skip(i).Aggregate(current, (path, part) => path.Combine(part));
                next = current.Combine(match);
            }

            current = next;
        }

        return current;
    }
}
