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

    public async System.Threading.Tasks.Task<bool> ShowConfirmation(string title, string message)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.YesNo, Icon.Question);
            var result = await box.ShowAsync();
            return result == ButtonResult.Yes;
        });
    }
}
