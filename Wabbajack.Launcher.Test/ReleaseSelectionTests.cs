using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Launcher.Platforms;
using Xunit;

namespace Wabbajack.Launcher.Test;

/// <summary>
///     Which file each launcher downloads from a release shaped exactly as the release workflows publish it:
///     the Windows zip and launcher, the Linux tarball and launcher, and a beta prerelease beside them.
///     <para>
///         Launchers already installed are never updated, so what matters most is that every one of them still
///         takes the Windows zip and nothing else. <see cref="OldLaunchers" /> is their selection code, copied:
///         the GitHub rule has been the same in every launcher since 2020, and the Nexus rule since 2022, the
///         2026 launchers adding category 3 to category 5. A test here failing means a release would hand some
///         installed launcher the wrong file.
///     </para>
/// </summary>
public class ReleaseSelectionTests
{
    private static ReleaseFile File(string name) => new(name, new Uri("https://example.invalid/" + name), 1);

    private static ReleaseInfo Release(string tag, bool prerelease, params string[] files) =>
        new(tag, prerelease, files.Select(File).ToList());

    /// <summary>The GitHub release list as the API returns it, newest first.</summary>
    private static readonly IReadOnlyList<ReleaseInfo> GitHub =
    [
        // A beta handed out by hand: newer than the release, and a prerelease.
        Release("4.4.1.0-beta.1", true, "4.4.1.0-beta.1-windows-x64.zip", "4.4.1.0-beta.1-linux-x64.tar.gz"),
        // A release with Linux builds attached, as Create Release publishes it.
        Release("4.4.0.0", false, "4.4.0.0.zip", "Wabbajack.exe", "4.4.0.0-linux-x64.tar.gz",
            "Wabbajack-linux-x64.tar.gz"),
        // One from before Linux builds.
        Release("4.3.1.0", false, "4.3.1.0.zip", "Wabbajack.exe")
    ];

    /// <summary>The mod page's files on Nexus, Linux ones listed first: (name, category).</summary>
    private static readonly IReadOnlyList<(string Name, int Category)> Nexus =
    [
        ("Wabbajack-linux-x64.tar.gz", 1),
        ("4.4.0.0-linux-x64.tar.gz", 3),
        ("Wabbajack.zip", 1),
        ("4.4.0.0.zip", 3)
    ];

    [Fact]
    public void OldLaunchersTakeTheWindowsZipFromGitHub()
    {
        Assert.Equal("4.4.0.0.zip", OldLaunchers.FromGitHub(GitHub));
    }

    [Fact]
    public void OldLaunchersNeverTakeALinuxFileFromNexus()
    {
        Assert.Equal("4.4.0.0.zip", OldLaunchers.FromNexus(Nexus, [5, 3]));
        Assert.Null(OldLaunchers.FromNexus(Nexus, [5]));
    }

    [Fact]
    public void TheWindowsLauncherTakesTheWindowsZip()
    {
        var picked = new WindowsLauncherPlatform().PickGitHubRelease(GitHub, out _);

        Assert.Equal((new Version(4, 4, 0, 0), "4.4.0.0.zip"), (picked!.Value.Version, picked.Value.File.Name));
    }

    [Fact]
    public void TheLinuxLauncherTakesTheLinuxTarballAndNoBeta()
    {
        var picked = LinuxLauncherPlatform.Pick(GitHub);

        Assert.Equal((new Version(4, 4, 0, 0), "4.4.0.0-linux-x64.tar.gz"),
            (picked!.Value.Version, picked.Value.File.Name));
    }

    /// <summary>Until a release has a Linux build, the Linux launcher takes none rather than a Windows one.</summary>
    [Fact]
    public void TheLinuxLauncherSkipsReleasesWithoutALinuxBuild()
    {
        Assert.Null(LinuxLauncherPlatform.Pick(GitHub.Where(r => r.Tag != "4.4.0.0")));
    }

    private static IReadOnlyList<NexusFile> NexusFiles =>
        Nexus.Select((f, i) => new NexusFile(f.Name, f.Category, i + 1, 1)).ToList();

    [Fact]
    public void TheWindowsLauncherTakesTheWindowsZipFromNexus()
    {
        Assert.Equal("4.4.0.0.zip", new WindowsLauncherPlatform().PickNexusFile(NexusFiles)!.Value.File.Name);
    }

    /// <summary>Linux downloads go through Nexus too, which counts them on the mod page, and take only the tarball.</summary>
    [Fact]
    public void TheLinuxLauncherTakesTheLinuxTarballFromNexus()
    {
        var files = NexusFiles.Append(new NexusFile("4.3.1.0-linux-x64.tar.gz", 4, 99, 1)).ToList();

        var picked = new LinuxLauncherPlatform().PickNexusFile(files);

        Assert.Equal((new Version(4, 4, 0, 0), "4.4.0.0-linux-x64.tar.gz"),
            (picked!.Value.Version, picked.Value.File.Name));
    }

    [Fact]
    public void TheLinuxLauncherTakesNothingFromNexusWithoutALinuxBuild()
    {
        Assert.Null(new LinuxLauncherPlatform().PickNexusFile(
            NexusFiles.Where(f => !f.Name.EndsWith(".tar.gz") || f.Name.StartsWith("Wabbajack")).ToList()));
    }

    /// <summary>The selection code of every launcher already installed, as it was written.</summary>
    private static class OldLaunchers
    {
        public static string? FromGitHub(IEnumerable<ReleaseInfo> releases)
        {
            var version = releases.Select(r =>
                {
                    if (r.Tag.Split(".").Length == 4 && Version.TryParse(r.Tag, out var v))
                        return (v, r);
                    return (new Version(0, 0, 0, 0), r);
                })
                .OrderByDescending(r => r.Item1)
                .FirstOrDefault();
            return version.r.Files.FirstOrDefault(a => a.Name == version.Item1 + ".zip")?.Name;
        }

        public static string? FromNexus(IEnumerable<(string Name, int Category)> files, int[] categories)
        {
            return files.Where(f => categories.Contains(f.Category))
                .Where(f => f.Name.EndsWith(".zip"))
                .Select(f => Version.TryParse(f.Name[..^4], out _) ? f.Name : null)
                .FirstOrDefault(f => f != null);
        }
    }
}
