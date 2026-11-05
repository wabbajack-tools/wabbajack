using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
///     A Linux desktop, reached through the freedesktop.org conventions every desktop environment follows:
///     xdg-open, xdg-mime, xdg-terminal-exec, and zenity or kdialog for a dialog outside the app's own window.
/// </summary>
public sealed class LinuxDesktop : IDesktop
{
    // There is no system message box to match, so once the window is up the app's own pane is used.
    public bool UsesNativeMessageBoxes => false;

    public void ShowNativeError(string message, string title)
    {
        // Also on stderr, since a dialog tool that is installed can still fail to reach the display.
        Console.Error.WriteLine($"{title}: {message}");
        if (!RunDialog("zenity", "--error", "--no-markup", "--title", title, "--text", message))
            RunDialog("kdialog", "--title", title, "--error", message);
    }

    /// <summary>Never asked before the window is up, which is all the native box is for; reads as no.</summary>
    public bool AskNativeYesNo(string message, string title)
    {
        return false;
    }

    // Running as root leaves the install owned by root.
    public bool IsElevated => Environment.IsPrivilegedProcess;

    // Selecting a file is Explorer's; the folder holding it is as close as xdg-open gets.
    public void ShowInFolder(AbsolutePath file)
    {
        UIUtils.OpenFolder(file.Parent);
    }

    /// <summary>
    ///     What cmd /k does on Windows, through xdg-terminal-exec, the desktop-neutral way to ask for a terminal.
    ///     Without one, the folder is opened, which is where the user would start the CLI from.
    /// </summary>
    public void OpenCliTerminal(string cliFolder, string cliFileName)
    {
        var info = new ProcessStartInfo("xdg-terminal-exec") {UseShellExecute = false, WorkingDirectory = cliFolder};
        foreach (var arg in new[] {"sh", "-c", $"./{cliFileName} -h; exec \"${{SHELL:-sh}}\""})
            info.ArgumentList.Add(arg);
        try
        {
            Process.Start(info)?.Dispose();
        }
        catch (Win32Exception)
        {
            UIUtils.OpenFolder(cliFolder.ToAbsolutePath());
        }
    }

    // MO2 runs under Wine or Proton, started however the user set that up, so there is no one shortcut to make.
    public bool CanCreateDesktopShortcut => false;

    public void CreateDesktopShortcut(string name, AbsolutePath target)
    {
        throw new NotSupportedException("No desktop shortcut is made on Linux");
    }

    // Avalonia extends into the title bar only on Windows and macOS; here the window manager's bar is turned
    // off, leaving the border to resize by, or both would show.
    public SystemDecorations WindowDecorations => SystemDecorations.BorderOnly;

    public SystemParameters? SystemParameters(IServiceProvider services)
    {
        return PortableSystemParameters.Create();
    }

    public AbsolutePath? ModlistsRoot(long installedSize)
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToAbsolutePath();
    }

    public void RegisterFileAssociations(IServiceProvider services)
    {
        ActivatorUtilities.CreateInstance<XdgFileAssociation>(services).RegisterOrUpdate();
    }

    public string LauncherAssetName => "Wabbajack-linux-x64.tar.gz";

    public bool LauncherAssetIsTheLauncher => false;

    /// <summary>
    ///     Takes the launcher out of the tarball (Wabbajack/Wabbajack), makes it executable, and renames it over the
    ///     old one, which has exited by the time the app it started runs.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    public async Task InstallLauncher(AbsolutePath asset, AbsolutePath launcher, CancellationToken token)
    {
        var replacement = launcher + ".new";
        await using (var file = File.OpenRead(asset.ToString()))
        await using (var gzip = new GZipStream(file, CompressionMode.Decompress))
        await using (var tar = new TarReader(gzip))
        {
            while (await tar.GetNextEntryAsync(cancellationToken: token) is { } entry)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) ||
                    entry.Name.TrimStart('.', '/') != "Wabbajack/Wabbajack")
                    continue;
                await entry.ExtractToFileAsync(replacement, true, token);
                File.SetUnixFileMode(replacement, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                                  UnixFileMode.UserExecute | UnixFileMode.GroupRead |
                                                  UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                                                  UnixFileMode.OtherExecute);
                File.Move(replacement, launcher.ToString(), true);
                return;
            }
        }

        throw new InvalidDataException("The launcher tarball holds no Wabbajack/Wabbajack");
    }

    /// <summary>Runs a desktop dialog tool and waits for it to close; false when the tool is not installed.</summary>
    private static bool RunDialog(string program, params string[] args)
    {
        var info = new ProcessStartInfo(program) {UseShellExecute = false};
        foreach (var arg in args) info.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(info);
            if (process == null) return false;
            process.WaitForExit();
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
