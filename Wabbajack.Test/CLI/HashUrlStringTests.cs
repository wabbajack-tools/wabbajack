using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.CLI.Verbs;
using Xunit;

namespace Wabbajack.CLI.Test;

public class HashUrlStringTests
{
    [Fact]
    public async Task Run_WithValidUrl_ReturnsZero()
    {
        var verb = new HashUrlString(NullLogger<HashUrlString>.Instance);
        var result = await verb.Run("https://www.nexusmods.com/skyrimspecialedition/mods/12345");
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_WithEmptyString_ReturnsZero()
    {
        var verb = new HashUrlString(NullLogger<HashUrlString>.Instance);
        var result = await verb.Run("");
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_WithDifferentStrings_ReturnsZero()
    {
        var verb = new HashUrlString(NullLogger<HashUrlString>.Instance);
        Assert.Equal(0, await verb.Run("test-string-1"));
        Assert.Equal(0, await verb.Run("test-string-2"));
        Assert.Equal(0, await verb.Run("https://example.com/path?query=value"));
    }
}
