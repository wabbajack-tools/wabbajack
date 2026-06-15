using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Wabbajack;

public sealed class AvaloniaDialogService : IDialogService
{
    public void ShowError(string message, string title)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Error);
        Dispatcher.UIThread.Post(() => _ = box.ShowAsync());
    }
}
