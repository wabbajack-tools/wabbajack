using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     The metrics key is created on first use, and every Wabbajack API call asks for it. On a fresh machine the
///     first calls arrive together, and each used to find no key and try to create one, failing with "the file
///     is being used by another process".
/// </summary>
public class WabbajackApiTokenProviderTests
{
    [Fact]
    public async Task FirstCallsArrivingTogether_AllGetTheSameKey()
    {
        var manager = new TemporaryFileManager();
        for (var round = 0; round < 20; round++)
        {
            await using var folder = manager.CreateFolder();
            var providers = Enumerable.Range(0, 8).Select(_ => new WabbajackApiTokenProvider(folder.Path)).ToArray();

            var keys = await Task.WhenAll(Enumerable.Range(0, 64)
                .Select(i => Task.Run(async () => (await providers[i % providers.Length].Get())!.MetricsKey)));

            Assert.Single(keys.Distinct());
            Assert.True(folder.Path.Combine("encrypted", "metrics-key").FileExists());
        }
    }

    [Fact]
    public async Task KeyOnDisk_IsReused()
    {
        var manager = new TemporaryFileManager();
        await using var folder = manager.CreateFolder();

        var first = (await new WabbajackApiTokenProvider(folder.Path).Get())!.MetricsKey;
        var second = (await new WabbajackApiTokenProvider(folder.Path).Get())!.MetricsKey;

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }
}
