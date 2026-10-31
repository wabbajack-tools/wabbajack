using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
/// Errors and yes-or-no questions from anywhere in the app. Where the desktop has a message box of its own that the
/// app is meant to look like (Windows), that is used throughout; elsewhere the app's floating pane is used once the
/// main window is up, and the desktop's fallback before that.
/// </summary>
public static class NativeMessageBox
{
    public static void ShowError(string message, string title)
    {
        if (Desktop.Current.UsesNativeMessageBoxes)
        {
            Desktop.Current.ShowNativeError(message, title);
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowError(message, title));
            return;
        }

        if (MainWindowVM() is { } vm)
            _ = vm.ShowErrorDialog(title, message);
        else
            Desktop.Current.ShowNativeError(message, title);
    }

    /// <summary>A question asked before the main window is up; true when the answer is yes.</summary>
    public static bool AskYesNo(string message, string title)
    {
        return Desktop.Current.AskNativeYesNo(message, title);
    }

    /// <summary>A question asked while the main window is up.</summary>
    public static Task<bool> AskYesNoAsync(string message, string title)
    {
        if (Desktop.Current.UsesNativeMessageBoxes || MainWindowVM() is not { } vm)
            return Task.FromResult(AskYesNo(message, title));
        return vm.ShowConfirmationDialog(title, message);
    }

    /// <summary>The main window's view model, when the main window is showing it.</summary>
    private static MainWindowVM? MainWindowVM()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        return window is {IsVisible: true} ? window.DataContext as MainWindowVM : null;
    }
}
