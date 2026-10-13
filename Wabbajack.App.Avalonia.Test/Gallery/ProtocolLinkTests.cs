using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Gallery;

/// <summary>
///     How a <c>wabbajack://</c> link from the website or Nexus Mods is routed. Browsers mangle a URL inside a
///     custom-scheme link, percent-escaping it or dropping the colon from <c>https://</c>; the repair used to run
///     only after the link had been classified, so a mangled download link without a .wabbajack ending was looked
///     up in the gallery instead and reported as not found.
/// </summary>
public class ProtocolLinkTests
{
    [Theory]
    [InlineData("nexus-collection/abc123", "abc123", null)]
    [InlineData("nexus-collection/abc123/revision/7", "abc123", 7)]
    [InlineData("/nexus-collection/abc123/revision/7/", "abc123", 7)]
    [InlineData("NEXUS-COLLECTION/abc123/REVISION/2", "abc123", 2)]
    [InlineData("nexus-collection/abc123/revision/latest", "abc123", null)]
    public void NexusCollection(string payload, string slug, int? revision)
    {
        Assert.Equal(new ProtocolLink.NexusCollection(slug, revision), ProtocolLink.Parse(payload));
    }

    [Theory]
    [InlineData("https://example.com/lists/My%20List.wabbajack", "https://example.com/lists/My List.wabbajack", "My List.wabbajack")]
    [InlineData("https://example.com/download?id=5", "https://example.com/download?id=5", "download.wabbajack")]
    [InlineData("https//example.com/lists/list.wabbajack", "https://example.com/lists/list.wabbajack", "list.wabbajack")]
    [InlineData("https//example.com/download", "https://example.com/download", "download.wabbajack")]
    [InlineData("https%3A%2F%2Fexample.com%2Fdownload", "https://example.com/download", "download.wabbajack")]
    [InlineData("http://example.com/x.WABBAJACK", "http://example.com/x.WABBAJACK", "x.WABBAJACK")]
    public void DirectDownload_IsRepairedBeforeItIsRecognised(string payload, string url, string fileName)
    {
        var link = Assert.IsType<ProtocolLink.DirectDownload>(ProtocolLink.Parse(payload));

        Assert.Equal(url, link.Url.ToString());
        Assert.Equal(fileName, link.FileName);
    }

    [Theory]
    [InlineData("halgari/halgaris_helper", "halgari/halgaris_helper", true)]
    [InlineData("halgaris_helper", "halgaris_helper", false)]
    [InlineData("  /halgari/halgaris_helper/ ", "halgari/halgaris_helper", true)]
    public void GalleryList(string payload, string id, bool namespaced)
    {
        Assert.Equal(new ProtocolLink.GalleryList(id, namespaced), ProtocolLink.Parse(payload));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("list.wabbajack")]
    [InlineData("ftp://example.com/list.wabbajack")]
    public void Unusable_IsInvalid(string? payload)
    {
        Assert.IsType<ProtocolLink.Invalid>(ProtocolLink.Parse(payload));
    }
}
