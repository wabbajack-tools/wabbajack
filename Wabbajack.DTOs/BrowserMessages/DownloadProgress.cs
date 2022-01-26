using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.BrowserMessages;

[JsonName("DownloadProgress")]
public class DownloadProgress : IMessage
{
    public bool IsDone { get; set; }
    public long BytesPerSecond { get; set; }
    public long BytesCompleted { get; set; }
}