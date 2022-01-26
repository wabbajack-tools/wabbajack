using System;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.BrowserMessages;

/// <summary>
/// Show a manual download page for the given Url
/// </summary>
[JsonName("ManualDownload")]
public class ManualDownload : IMessage
{
    public string Prompt { get; set; }
    public Uri Url { get; set; }
    public AbsolutePath Path { get; set; }
}