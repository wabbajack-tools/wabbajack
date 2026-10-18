using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Launcher.Platforms;

/// <summary>
///     The launcher on Windows, as it has always behaved, since launchers already installed cannot be updated and
///     every new one has to agree with them: a premium Nexus login first, then the GitHub release with the highest
///     four-number tag and its {tag}.zip, unpacked into a version folder in the working directory.
/// </summary>
public sealed class WindowsLauncherPlatform : ILauncherPlatform
{
    /// <summary>The first optional or miscellaneous file named {version}.zip, as every launcher since 2022 takes.</summary>
    public (Version Version, NexusFile File)? PickNexusFile(IReadOnlyList<NexusFile> files)
    {
        return files.Where(f => f.Category == 5 || f.Category == 3)
            .Where(f => f.Name.EndsWith(".zip"))
            .Select(f => Version.TryParse(f.Name[..^4], out var version) ? ((Version, NexusFile)?) (version, f) : null)
            .FirstOrDefault(f => f != null);
    }

    public AbsolutePath InstallRoot => KnownFolders.CurrentDirectory;

    public string AppExecutable => "Wabbajack.exe";

    public (Version Version, ReleaseFile File)? PickGitHubRelease(IReadOnlyList<ReleaseInfo> releases,
        out string whyNot)
    {
        var newest = releases
            .Select(r => (Version: r.Tag.Split(".").Length == 4 && Version.TryParse(r.Tag, out var v)
                ? v
                : new Version(0, 0, 0, 0), Release: r))
            .OrderByDescending(r => r.Version)
            .FirstOrDefault();

        var asset = newest.Release?.Files.FirstOrDefault(f => f.Name == newest.Version + ".zip");
        if (asset == null)
        {
            whyNot = $"Error, no asset found for Github Release {newest.Release}";
            return null;
        }

        whyNot = "";
        return (newest.Version, asset);
    }

    public void Unpack(byte[] release, AbsolutePath versionFolder)
    {
        using var zip = new ZipArchive(new MemoryStream(release), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            var outPath = versionFolder.Combine(entry.FullName.ToRelativePath());
            if (!outPath.Parent.DirectoryExists())
                outPath.Parent.CreateDirectory();

            if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                continue;
            using var o = entry.Open();
            using var of = outPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            o.CopyTo(of);
        }
    }

    /// <summary>wabbajack-cli.bat beside the launcher.</summary>
    public async Task WriteCliShortcut(AbsolutePath app)
    {
        var cli = app.Parent.Combine("cli", "wabbajack-cli.exe");
        var data = $"\"{cli}\" %*";
        var file = InstallRoot.Combine("wabbajack-cli.bat").ToString();
        if (File.Exists(file) && await File.ReadAllTextAsync(file) == data) return;
        await File.WriteAllTextAsync(file, data);
    }

    public ProcessStartInfo StartApp(AbsolutePath app, IReadOnlyList<string> args)
    {
        return new ProcessStartInfo
        {
            FileName = app.ToString(),
            Arguments = string.Join(" ", args.Select(s => s.Contains(' ') ? '\"' + s + '\"' : s)),
            WorkingDirectory = app.Parent.ToString()
        };
    }
}
