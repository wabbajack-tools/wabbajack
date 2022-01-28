using Microsoft.JSInterop;

namespace Wabbajack.App.Blazor.Utility;

public static class JsInterop
{
    /// <summary>
    /// Converts a <see cref="DotNetStreamReference"/> into a blob URL. Useful for streaming images.
    /// <code>async function getBlobUrlFromStream(imageStream: DotNetStreamReference)</code>
    /// </summary>
    public const string GetBlobUrlFromStream = "getBlobUrlFromStream";
}
