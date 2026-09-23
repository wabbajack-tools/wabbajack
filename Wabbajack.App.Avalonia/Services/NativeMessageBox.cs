using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
/// WPF's MessageBox.Show is the Windows message box, so this calls the same one: same look, same sounds,
/// owned by the main window so it stays in front of it.
/// </summary>
public static class NativeMessageBox
{
    private const uint MbOk = 0x0;
    private const uint MbIconError = 0x10;

    public static void ShowError(string message, string title)
    {
        if (!OperatingSystem.IsWindows()) return;

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        MessageBoxW(owner, message, title, MbOk | MbIconError);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
