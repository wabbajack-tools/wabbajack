using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI.Test;

public class HashUrlStringTests
{
    [Test]
    public async Task Run_WithValidUrl_ReturnsZero()
    {
        var verb = new HashUrlString(NullLogger<HashUrlString>.Instance);
        var result = await verb.Run("https://www.nexusmods.com/skyrimspecialedition/mods/12345");
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Run_WithEmptyString_ReturnsZero()
    {
        var verb = new HashUrlString(NullLogger<HashUrlString>.Instance);
        var result = await verb.Run("");
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Run_WithDifferentStrings_ReturnsZero()
    {
        var verb = new HashUrlString(NullLogger<HashUrlString>.Instance);
        await Assert.That(await verb.Run("test-string-1")).IsEqualTo(0);
        await Assert.That(await verb.Run("test-string-2")).IsEqualTo(0);
        await Assert.That(await verb.Run("https://example.com/path?query=value")).IsEqualTo(0);
    }
}
