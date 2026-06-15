using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Wabbajack;

public sealed class AvaloniaDialogService : IDialogService
{
    public void ShowError(string message, string title)
    {
        // Fire-and-forget; the MessageBox shows on the UI thread.
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Error);
        _ = box.ShowAsync();
    }
}
