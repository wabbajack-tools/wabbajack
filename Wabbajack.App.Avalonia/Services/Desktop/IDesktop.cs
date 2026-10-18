using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Wabbajack.Installer;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
///     What the desktop the app runs on does for it: message boxes, the file manager, a terminal, shortcuts, file
///     associations, the window frame, where lists go. Everything that differs between Windows and Linux at the
///     level of the user's desktop is answered here, by <see cref="Desktop.Current" />, rather than by checking
///     the platform where it is used.
/// </summary>
public interface IDesktop
{
    /// <summary>
    ///     Whether errors and questions go to the platform's own message box even while the main window is up, as
    ///     the WPF app's did on Windows. Elsewhere the app's floating pane is used once there is a window.
    /// </summary>
    bool UsesNativeMessageBoxes { get; }

    /// <summary>An error in the platform's own message box, for when there is no window to show it in.</summary>
    void ShowNativeError(string message, string title);

    /// <summary>A yes-or-no question in the platform's own message box; true when the answer is yes.</summary>
    bool AskNativeYesNo(string message, string title);

    /// <summary>Running as an administrator, or as root: installs made that way end up owned by them.</summary>
    bool IsElevated { get; }

    /// <summary>Shows a file in the file manager, selected where the file manager can do that.</summary>
    void ShowInFolder(AbsolutePath file);

    /// <summary>A terminal in the CLI's folder, showing the CLI's help and left open for the user.</summary>
    void OpenCliTerminal(string cliFolder, string cliFileName);

    /// <summary>Whether a desktop shortcut to an installed list's MO2 can be made here.</summary>
    bool CanCreateDesktopShortcut { get; }

    void CreateDesktopShortcut(string name, AbsolutePath target);

    /// <summary>The frame the window asks the window manager for; the window draws its own title bar.</summary>
    SystemDecorations WindowDecorations { get; }

    /// <summary>Screen size and video memory for the settings an install writes; null when they cannot be read.</summary>
    SystemParameters? SystemParameters(IServiceProvider services);

    /// <summary>The folder a Modlists folder is suggested in, for a list of this installed size; null for none.</summary>
    AbsolutePath? ModlistsRoot(long installedSize);

    /// <summary>Opens .wabbajack files and wabbajack:// links with this executable, wherever it now lives.</summary>
    void RegisterFileAssociations(IServiceProvider services);

    /// <summary>The release asset this platform's launcher comes in.</summary>
    string LauncherAssetName { get; }

    /// <summary>Whether that asset is the launcher itself, so the two can be compared directly.</summary>
    bool LauncherAssetIsTheLauncher { get; }

    /// <summary>Replaces the launcher with the one in a downloaded launcher asset.</summary>
    Task InstallLauncher(AbsolutePath asset, AbsolutePath launcher, CancellationToken token);
}

public static class Desktop
{
    /// <summary>
    ///     This platform's desktop. Anything that is not Windows is treated as a Linux desktop: macOS has no
    ///     desktop of its own here yet, and gets one when it is supported.
    /// </summary>
    public static IDesktop Current { get; } = OperatingSystem.IsWindows() ? new WindowsDesktop() : new LinuxDesktop();
}
