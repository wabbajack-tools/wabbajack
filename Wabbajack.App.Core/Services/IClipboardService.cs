namespace Wabbajack;

/// <summary>Platform abstraction for writing text to the system clipboard.</summary>
public interface IClipboardService
{
    System.Threading.Tasks.Task SetTextAsync(string text);
}
