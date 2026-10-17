using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Wabbajack.Paths;

namespace Wabbajack.Launcher.Platforms;

/// <summary>A release's tag, whether it is a prerelease, and its files by name.</summary>
public record ReleaseInfo(string Tag, bool Prerelease, IReadOnlyList<ReleaseFile> Files);

public record ReleaseFile(string Name, Uri Url, long Size);

/// <summary>
///     Everything the launcher does differently on Windows and Linux: which releases it takes and from where, how
///     it unpacks one, where version folders live, and how it starts the app. The flow itself (check, download,
///     unpack, launch) is the same on both and lives in the view model.
/// </summary>
public interface ILauncherPlatform
{
    /// <summary>Whether a premium Nexus login is asked for the release before GitHub is.</summary>
    bool ChecksNexus { get; }

    /// <summary>The folder version folders are kept in.</summary>
    AbsolutePath InstallRoot { get; }

    /// <summary>The app's file name inside a version folder.</summary>
    string AppExecutable { get; }

    /// <summary>The release to install from GitHub's list, or null with what to tell the user.</summary>
    (Version Version, ReleaseFile File)? PickGitHubRelease(IReadOnlyList<ReleaseInfo> releases, out string whyNot);

    /// <summary>Unpacks a downloaded release into its version folder.</summary>
    void Unpack(byte[] release, AbsolutePath versionFolder);

    /// <summary>Writes the wabbajack-cli shortcut beside the launcher, pointing at this version's CLI.</summary>
    Task WriteCliShortcut(AbsolutePath app);

    /// <summary>How to start the app, passing on what the launcher was started with.</summary>
    ProcessStartInfo StartApp(AbsolutePath app, IReadOnlyList<string> args);
}

public static class LauncherPlatform
{
    public static ILauncherPlatform Current { get; } = OperatingSystem.IsWindows()
        ? new WindowsLauncherPlatform()
        : new LinuxLauncherPlatform();
}
