using System;
using System.Text.Json;
using Wabbajack.DTOs.CDN;
using Wabbajack.Hashing.xxHash64;
using Xunit;

namespace Wabbajack.DTOs.Test;

public class ProxyIndexTests
{
    // What the proxy's filler Worker writes to index.json.
    private const string Served =
        """{"Version":1,"Generated":"2026-10-17T23:46:13.462Z","Files":{"02271cd091c54282":427562906,"ce40df44e3c6f511":998789120}}""";

    private static ProxyIndex Load()
    {
        // HttpClient.GetFromJsonAsync reads with the web defaults.
        var index = JsonSerializer.Deserialize<ProxyIndex>(Served, JsonSerializerOptions.Web)!;
        index.BaseUrl = new Uri("https://proxy-v2.wabbajack.org/");
        return index;
    }

    [Fact]
    public void ReadsWhatTheProxyServes()
    {
        var index = Load();

        Assert.Equal(1, index.Version);
        Assert.Equal(2, index.Files.Count);
        Assert.True(index.Holds(Hash.FromHex("ce40df44e3c6f511"), 998789120));
    }

    [Fact]
    public void HoldsOnlyTheSameHashAtTheSameSize()
    {
        var index = Load();

        Assert.False(index.Holds(Hash.FromHex("ce40df44e3c6f511"), 998789121));
        Assert.False(index.Holds(Hash.FromHex("0000000000000001"), 998789120));
        Assert.False(index.Holds(default, 0));
    }

    [Fact]
    public void AnIndexFromNowhereHoldsNothing()
    {
        Assert.False(ProxyIndex.Empty.Holds(Hash.FromHex("ce40df44e3c6f511"), 998789120));
    }

    [Fact]
    public void FilesLiveUnderTheirHash()
    {
        Assert.Equal("https://proxy-v2.wabbajack.org/ce40df44e3c6f511",
            Load().UrlFor(Hash.FromHex("ce40df44e3c6f511")).ToString());
    }
}
