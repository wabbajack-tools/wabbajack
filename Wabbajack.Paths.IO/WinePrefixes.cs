using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wabbajack.Paths.IO;

/// <summary>A Windows path read back through a Wine prefix: where it is here, and the prefix that said so.</summary>
public readonly record struct WinePath(AbsolutePath Path, AbsolutePath Prefix);

/// <summary>
///     The Wine and Proton prefixes on this machine, and what a Windows program's drive letters mean in them.
///     <para>
///         <see cref="WindowsProgramPaths" /> handles Z:, which every prefix maps to the Unix root. Any other
///         letter belongs to one prefix: each has its own C: (its drive_c folder), and Proton adds others, such
///         as S: for the Steam folder. A path a program wrote on one of those drives means nothing until the
///         prefix is known, and nothing records which prefix that was, so every prefix is asked.
///     </para>
/// </summary>
public static partial class WinePrefixes
{
    /// <summary>
    ///     Every prefix that can be found: $WINEPREFIX, ~/.wine, and each Steam library's Proton prefixes, for
    ///     native, Flatpak and Snap Steam alike.
    /// </summary>
    public static IReadOnlyList<AbsolutePath> Find()
    {
        if (OperatingSystem.IsWindows()) return Array.Empty<AbsolutePath>();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new List<string>();
        if (Environment.GetEnvironmentVariable("WINEPREFIX") is {Length: > 0} winePrefix) candidates.Add(winePrefix);
        candidates.Add(Path.Combine(home, ".wine"));

        foreach (var library in SteamLibraries(home))
        {
            var compatData = Path.Combine(library, "steamapps", "compatdata");
            if (!Directory.Exists(compatData)) continue;
            candidates.AddRange(Directory.EnumerateDirectories(compatData).Select(d => Path.Combine(d, "pfx")));
        }

        return candidates
            .Where(p => Directory.Exists(Path.Combine(p, "dosdevices")))
            .Select(RealPath)
            .Distinct(StringComparer.Ordinal)
            .Select(p => p.ToAbsolutePath())
            .ToList();
    }

    /// <summary>
    ///     A path a Windows program such as MO2 wrote, as this machine names it. On Windows that is the path; under
    ///     Wine a Z: path is a Unix one, and any other drive letter is looked up in the prefixes found here, which
    ///     <see cref="WinePath.Prefix" /> then names. Default when it is on a drive no prefix has.
    /// </summary>
    public static WinePath FromWindowsProgramPath(string? written)
    {
        var path = WindowsProgramPaths.FromWindowsProgramPath(written);
        if (OperatingSystem.IsWindows() || path == default || path.PathFormat == PathFormat.Unix)
            return new WinePath(path, default);
        return Resolve(written, Find()) ?? default;
    }

    /// <summary>
    ///     <paramref name="windowsPath" /> as it is on this machine, through whichever of
    ///     <paramref name="prefixes" /> has it; null when it is not a drive path, or no prefix has it. Where
    ///     several do, as every prefix has a C:\users\steamuser\Downloads, one where it holds something wins over
    ///     one where it is empty, and then the most recently written.
    /// </summary>
    public static WinePath? Resolve(string? windowsPath, IEnumerable<AbsolutePath> prefixes)
    {
        if (windowsPath is not {Length: >= 3} || !char.IsAsciiLetter(windowsPath[0]) || windowsPath[1] != ':' ||
            windowsPath[2] is not ('\\' or '/'))
            return null;

        var drive = char.ToLowerInvariant(windowsPath[0]) + ":";
        var rest = windowsPath[3..].Replace('\\', '/').Trim('/');

        var found = new List<(WinePath Path, bool HasContent, DateTime Written)>();
        foreach (var prefix in prefixes)
        {
            var root = DriveRoot(prefix, drive);
            if (root == null) continue;

            var path = rest.Length == 0 ? root.Value : root.Value.FindIgnoringCase(rest.ToRelativePath());
            if (Directory.Exists(path.ToString()))
                found.Add((new WinePath(path, prefix), Directory.EnumerateFileSystemEntries(path.ToString()).Any(),
                    Directory.GetLastWriteTimeUtc(path.ToString())));
            else if (File.Exists(path.ToString()))
                found.Add((new WinePath(path, prefix), true, File.GetLastWriteTimeUtc(path.ToString())));
        }

        return found.Count == 0
            ? null
            : found.OrderByDescending(f => f.HasContent).ThenByDescending(f => f.Written).First().Path;
    }

    /// <summary>Where a drive letter of a prefix points: its dosdevices entry, a link relative to that folder.</summary>
    private static AbsolutePath? DriveRoot(AbsolutePath prefix, string drive)
    {
        var dosDevices = Path.Combine(prefix.ToString(), "dosdevices");
        var link = new DirectoryInfo(Path.Combine(dosDevices, drive));
        if (!link.Exists) return null;
        var target = link.LinkTarget;
        return (target == null ? link.FullName : Path.GetFullPath(target, dosDevices)).ToAbsolutePath();
    }

    /// <summary>
    ///     The Steam libraries of every Steam install in its usual place: the install itself and each folder its
    ///     libraryfolders.vdf lists.
    /// </summary>
    private static IEnumerable<string> SteamLibraries(string home)
    {
        var roots = new[]
        {
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"),
            Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam")
        };

        var libraries = new List<string>();
        foreach (var root in roots.Where(Directory.Exists))
        {
            libraries.Add(root);
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            libraries.AddRange(LibraryPath().Matches(File.ReadAllText(vdf))
                .Select(m => m.Groups[1].Value.Replace(@"\\", @"\")));
        }

        return libraries.Where(Directory.Exists).Select(RealPath).Distinct(StringComparer.Ordinal);
    }

    private static string RealPath(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            return info.ResolveLinkTarget(true)?.FullName ?? info.FullName;
        }
        catch (IOException)
        {
            return path;
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPath();
}
