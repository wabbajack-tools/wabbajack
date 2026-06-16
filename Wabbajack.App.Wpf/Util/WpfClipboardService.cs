using System.Threading.Tasks;
namespace Wabbajack;
public sealed class WpfClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        System.Windows.Clipboard.SetText(text);
        return Task.CompletedTask;
    }
}
