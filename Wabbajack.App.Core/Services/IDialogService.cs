namespace Wabbajack;

/// <summary>
/// Platform abstraction for simple modal dialogs, so ViewModels don't reference WPF's MessageBox.
/// The WPF app supplies a MessageBox-backed implementation; an Avalonia app would supply its own.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a modal error dialog with an OK button.</summary>
    void ShowError(string message, string title);
}
