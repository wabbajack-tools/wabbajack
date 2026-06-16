using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
namespace Wabbajack;
public sealed class AvaloniaClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var top = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (top?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }
}
