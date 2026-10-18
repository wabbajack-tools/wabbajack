using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>The Windows desktop, doing what the WPF app did there.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDesktop : IDesktop
{
    // WPF's MessageBox.Show is the Windows message box, so this calls the same one: same look, same sounds,
    // owned by the main window so it stays in front of it.
    public bool UsesNativeMessageBoxes => true;

    public void ShowNativeError(string message, string title)
    {
        MessageBoxW(OwnerHandle(), message, title, MbOk | MbIconError);
    }

    public bool AskNativeYesNo(string message, string title)
    {
        return MessageBoxW(OwnerHandle(), message, title, MbYesNo | MbIconInformation) == IdYes;
    }

    public bool IsElevated
    {
        get
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                if (identity.Owner is { } owner) return owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public void ShowInFolder(AbsolutePath file)
    {
        Process.Start(new ProcessStartInfo {FileName = "explorer.exe ", Arguments = $"/select, \"{file}\""});
    }

    public void OpenCliTerminal(string cliFolder, string cliFileName)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = cliFolder,
            Arguments = $"/k \"{cliFileName} -h\""
        });
    }

    public bool CanCreateDesktopShortcut => true;

    /// <summary>An Internet Shortcut (.url) on the desktop, opening the target with its own icon.</summary>
    public void CreateDesktopShortcut(string name, AbsolutePath target)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        using var writer = new StreamWriter(Path.Combine(desktop, name + ".url"));
        var path = target.ToString();
        writer.WriteLine("[InternetShortcut]");
        writer.WriteLine("URL=file:///" + path);
        writer.WriteLine("IconIndex=0");
        writer.WriteLine("IconFile=" + path.Replace('\\', '/'));
    }

    // The window extends into the title bar, so the system's frame is kept and drawn over.
    public SystemDecorations WindowDecorations => SystemDecorations.Full;

    public SystemParameters? SystemParameters(IServiceProvider services)
    {
        return services.GetRequiredService<SystemParametersConstructor>().Create();
    }

    /// <summary>The root of the preferred drive: an SSD with room for the list, its media type read through WMI.</summary>
    public AbsolutePath? ModlistsRoot(long installedSize)
    {
        return DriveHelper.GetPreferredInstallationDrive(installedSize)?.RootDirectory.FullName.ToAbsolutePath();
    }

    public void RegisterFileAssociations(IServiceProvider services)
    {
        ActivatorUtilities.CreateInstance<FileAssociationSelfHealService>(services)
            .RegisterOrUpdate(enableProtocol: true);
    }

    public string LauncherAssetName => "Wabbajack.exe";

    public bool LauncherAssetIsTheLauncher => true;

    // The launcher has exited by the time the app it started runs, so its file is free to replace.
    public async Task InstallLauncher(AbsolutePath asset, AbsolutePath launcher, CancellationToken token)
    {
        await asset.MoveToAsync(launcher, true, token);
    }

    private static IntPtr OwnerHandle()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }

    private const uint MbOk = 0x0;
    private const uint MbIconError = 0x10;
    private const uint MbYesNo = 0x4;
    private const uint MbIconInformation = 0x40;
    private const int IdYes = 6;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
