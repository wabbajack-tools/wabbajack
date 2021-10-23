using System.Threading.Tasks;
using Shipwreck.Phash;
using Wabbajack.DTOs.Texture;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Hashing.PHash.Test;

public class FileLoadingTests
{
    [Theory]
    [InlineData("test-dxt5.dds", 1.0f)]
    [InlineData("test-dxt5-recompressed.dds", 1f)]
    [InlineData("test-dxt5-small-bc7.dds", 0.983f)]
    [InlineData("test-dxt5-small-bc7-vflip.dds", 0.189f)]
    public async Task LoadAllFiles(string file, float difference)
    {
        var baseState =
            await ImageLoader.Load("TestData/test-dxt5.dds".ToRelativePath().RelativeTo(KnownFolders.EntryPoint));
        var state = await ImageLoader.Load("TestData".ToRelativePath().Combine(file)
            .RelativeTo(KnownFolders.EntryPoint));

        Assert.NotEqual(DXGI_FORMAT.UNKNOWN, baseState.Format);

        Assert.Equal(difference,
            ImagePhash.GetCrossCorrelation(
                new Digest {Coefficients = baseState.PerceptualHash.Data},
                new Digest {Coefficients = state.PerceptualHash.Data}),
            1.0);
    }
}