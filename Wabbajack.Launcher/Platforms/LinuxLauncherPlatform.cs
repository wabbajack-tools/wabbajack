using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Launcher.Platforms;

/// <summary>
///     The launcher on Linux. A release carries the Windows build as <c>{version}.zip</c> and the Linux one as
///     <c>{version}-linux-x64.tar.gz</c>, on GitHub and on Nexus alike; this takes only the second, so it never
///     fetches a Windows build, and skips a release that has none, as every release before Linux builds existed
///     does. Prereleases are skipped too: betas are handed out by hand, never through a launcher.
/// </summary>
public sealed class LinuxLauncherPlatform : ILauncherPlatform
{
    /// <summary>
    ///     The newest file named {version}-linux-x64.tar.gz, in whatever category. No Windows launcher can take
    ///     it: they all want a name ending in .zip.
    /// </summary>
    public (Version Version, NexusFile File)? PickNexusFile(IReadOnlyList<NexusFile> files)
    {
        return files
            .Select(f => (File: f, Version: VersionOfLinuxBuild(f.Name)))
            .Where(f => f.Version != null)
            .OrderByDescending(f => f.Version)
            .Select(f => ((Version, NexusFile)?) (f.Version!, f.File))
            .FirstOrDefault();
    }

    /// <summary>The version a {version}-linux-x64.tar.gz names, or null for any other name.</summary>
    private static Version? VersionOfLinuxBuild(string name)
    {
        const string suffix = "-linux-x64.tar.gz";
        if (!name.EndsWith(suffix, StringComparison.Ordinal)) return null;
        var version = name[..^suffix.Length];
        return version.Split('.').Length == 4 && Version.TryParse(version, out var v) ? v : null;
    }

    // A desktop starts programs in the home folder, so version folders go beside the launcher instead.
    public AbsolutePath InstallRoot => KnownFolders.EntryPoint;

    public string AppExecutable => "Wabbajack";

    public static string AssetName(Version version) => $"{version}-linux-x64.tar.gz";

    public (Version Version, ReleaseFile File)? PickGitHubRelease(IReadOnlyList<ReleaseInfo> releases,
        out string whyNot)
    {
        var picked = Pick(releases);
        whyNot = picked == null ? "No release has a Linux build yet" : "";
        return picked;
    }

    /// <summary>The newest stable release with a Linux build, or null when there is none.</summary>
    public static (Version Version, ReleaseFile File)? Pick(IEnumerable<ReleaseInfo> releases)
    {
        return releases
            .Where(r => !r.Prerelease && r.Tag.Split('.').Length == 4)
            .Select(r => Version.TryParse(r.Tag, out var v)
                ? (Version: v, File: r.Files.FirstOrDefault(f => f.Name == AssetName(v)))
                : default)
            .Where(r => r.File != null)
            .OrderByDescending(r => r.Version)
            .Select(r => ((Version, ReleaseFile)?) (r.Version, r.File!))
            .FirstOrDefault();
    }

    [UnsupportedOSPlatform("windows")]
    public void Unpack(byte[] release, AbsolutePath versionFolder)
    {
        Extract(new MemoryStream(release), versionFolder.ToString());
    }

    /// <summary>
    ///     Unpacks a release tarball into <paramref name="destination" />. The tarball holds one top-level folder,
    ///     which is dropped, so the app lands directly in the version folder as the Windows zip's does. Files keep
    ///     their modes, which is what keeps the app, the CLI and the bundled tools executable. It is unpacked
    ///     beside the destination first and moved into place whole: a version folder that exists is taken as
    ///     installed, so a half-unpacked one must never be left under that name.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    public static void Extract(Stream tarGz, string destination)
    {
        var partial = destination + ".partial";
        if (Directory.Exists(partial)) Directory.Delete(partial, true);
        Directory.CreateDirectory(partial);

        using (var gzip = new GZipStream(tarGz, CompressionMode.Decompress))
        using (var reader = new TarReader(gzip))
        {
            while (reader.GetNextEntry() is { } entry)
            {
                var name = entry.Name.Replace('\\', '/');
                var slash = name.IndexOf('/');
                if (slash < 0) continue; // the top-level folder itself
                var relative = name[(slash + 1)..].TrimEnd('/');
                if (relative.Length == 0) continue;

                var path = Path.GetFullPath(Path.Combine(partial, relative));
                if (!path.StartsWith(partial + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidDataException($"{entry.Name} would unpack outside the version folder");

                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        Directory.CreateDirectory(path);
                        break;
                    case TarEntryType.RegularFile or TarEntryType.V7RegularFile:
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        entry.ExtractToFile(path, true);
                        File.SetUnixFileMode(path, entry.Mode);
                        break;
                }
            }
        }

        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        Directory.Move(partial, destination);
    }

    /// <summary>The counterpart of the Windows .bat: a wabbajack-cli script beside the launcher.</summary>
    [UnsupportedOSPlatform("windows")]
    public async Task WriteCliShortcut(AbsolutePath app)
    {
        var script = InstallRoot.Combine("wabbajack-cli").ToString();
        var cli = app.Parent.Combine("cli", "wabbajack-cli").ToString().Replace("'", "'\\''");
        var data = $"#!/bin/sh\nexec '{cli}' \"$@\"\n";
        if (!File.Exists(script) || await File.ReadAllTextAsync(script) != data)
            await File.WriteAllTextAsync(script, data);
        File.SetUnixFileMode(script, File.GetUnixFileMode(script) | UnixFileMode.UserExecute |
                                     UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    public ProcessStartInfo StartApp(AbsolutePath app, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(app.ToString()) {WorkingDirectory = app.Parent.ToString()};
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }
}
