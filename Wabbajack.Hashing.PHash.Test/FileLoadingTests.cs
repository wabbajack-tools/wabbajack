using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Shipwreck.Phash;
using Wabbajack.DTOs.Texture;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Hashing.PHash.Test;

public class FileLoadingTests : IAsyncDisposable
{
    private readonly IImageLoader[] _imageLoaders;
    private readonly TemporaryFileManager _tmp;

    public FileLoadingTests()
    {
        _tmp = new TemporaryFileManager();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            _imageLoaders = new IImageLoader[]
            {
                new CrossPlatformImageLoader(),
                new TexConvImageLoader(_tmp)
            };
        }
        else
        {
            _imageLoaders = new IImageLoader[]
            {
                new CrossPlatformImageLoader(),
            };
        }
    }
    
    [Theory]
    [InlineData("test-dxt5.dds", 1.0f)]
    [InlineData("test-dxt5-recompressed.dds", 1f)]
    [InlineData("test-dxt5-small-bc7.dds", 0.983f)]
    [InlineData("test-dxt5-small-bc7-vflip.dds", 0.189f)]
    public async Task LoadAllFiles(string file, float difference)
    {
        foreach (var imageLoader in _imageLoaders)
        {
            var baseState =
                await imageLoader.Load("TestData/test-dxt5.dds".ToRelativePath().RelativeTo(KnownFolders.EntryPoint));
            var state = await imageLoader.Load("TestData".ToRelativePath().Combine(file)
                .RelativeTo(KnownFolders.EntryPoint));

            Assert.NotEqual(DXGI_FORMAT.UNKNOWN, baseState.Format);

            Assert.Equal(difference,
                ImagePhash.GetCrossCorrelation(
                    new Digest { Coefficients = baseState.PerceptualHash.Data },
                    new Digest { Coefficients = state.PerceptualHash.Data }),
                1.0);
        }
    }

    [Fact]
    public async Task CanConvertCubeMaps()
    {
        foreach (var imageLoader in _imageLoaders)
        {
            // File used here via re-upload permissions found on the mod's Nexus page:
            // https://www.nexusmods.com/fallout4/mods/43458?tab=description
            // Used for testing purposes only
            var path = "TestData/WindowDisabled_CGPlayerHouseCube.dds".ToRelativePath().RelativeTo(KnownFolders.EntryPoint);
        
            var baseState = await imageLoader.Load(path);
            baseState.Height.Should().Be(128);
            baseState.Width.Should().Be(128);
            //baseState.Frames.Should().Be(6);

            using var ms = new MemoryStream();
            await using var ins = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            await imageLoader.Recompress(ins, 128, 128, baseState.MipLevels, DXGI_FORMAT.BC1_UNORM, ms, CancellationToken.None, leaveOpen:true);
            ms.Length.Should().Be(ins.Length);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _tmp.DisposeAsync();
    }
}