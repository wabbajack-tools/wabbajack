using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
/// WPF's MessageBox.Show is the Windows message box, so on Windows this calls the same one: same look, same
/// sounds, owned by the main window so it stays in front of it. Elsewhere there is no such box. Once the main
/// window is up, the app's own floating pane does the job; before that (or after it has gone) the desktop's
/// zenity or kdialog does, and failing both the message goes to stderr.
/// </summary>
public static class NativeMessageBox
{
    private const uint MbOk = 0x0;
    private const uint MbIconError = 0x10;

    public static void ShowError(string message, string title)
    {
        if (OperatingSystem.IsWindows())
        {
            MessageBoxW(OwnerHandle(), message, title, MbOk | MbIconError);
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowError(message, title));
            return;
        }

        if (MainWindowVM() is { } vm)
        {
            _ = vm.ShowErrorDialog(title, message);
            return;
        }

        if (RunDialog("zenity", "--error", "--no-markup", "--title", title, "--text", message) ||
            RunDialog("kdialog", "--title", title, "--error", message))
            return;

        Console.Error.WriteLine($"{title}: {message}");
    }

    /// <summary>MessageBox.Show with YesNo and the information icon; true when the answer is Yes.</summary>
    public static bool AskYesNo(string message, string title)
    {
        if (!OperatingSystem.IsWindows()) return false;
        return MessageBoxW(OwnerHandle(), message, title, MbYesNo | MbIconInformation) == IdYes;
    }

    /// <summary>
    ///     <see cref="AskYesNo" /> for a question asked while the main window is up, which off Windows is the
    ///     floating pane.
    /// </summary>
    public static Task<bool> AskYesNoAsync(string message, string title)
    {
        if (OperatingSystem.IsWindows() || MainWindowVM() is not { } vm)
            return Task.FromResult(AskYesNo(message, title));
        return vm.ShowConfirmationDialog(title, message);
    }

    private static IntPtr OwnerHandle()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }

    /// <summary>The main window's view model, when the main window is showing it.</summary>
    private static MainWindowVM? MainWindowVM()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        return window is {IsVisible: true} ? window.DataContext as MainWindowVM : null;
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

    private const uint MbYesNo = 0x4;
    private const uint MbIconInformation = 0x40;
    private const int IdYes = 6;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
