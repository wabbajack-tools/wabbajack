using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;

namespace Wabbajack.ImageHashing.Test
{
    public class ImageLoadingTests
    {
        [Fact]
        public async Task CanLoadAndCompareDDSImages()
        {
            var hash1 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            var state1 = await ImageState.GetState(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            
            Assert.Equal(512, state1.Width);
            Assert.Equal(512, state1.Height);
            Assert.Equal(DXGI_FORMAT.BC3_UNORM, state1.Format);

            var hash2 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));

            // From old embedded hashing method, we want to make sure the hashing algorithm hasn't changed so much that
            // we've broken the old caches
            var hash3 = PHash.FromBase64("cns+/2xel0ulcwCXeTlVW2x5aGtwaGl9glpthWZkb2ducnF0c2lvgQ==");
            
            
            Assert.Equal(1, hash1.Similarity(hash2));
            Assert.True(hash1.Similarity(hash3) > 0.99f);
        }
        
        [Fact]
        public async Task CanLoadAndCompareResizedImage()
        {
            var hash1 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            
            var hash2 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5-small-bc7.dds"));
            
            var state2 = await ImageState.GetState(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5-small-bc7.dds"));
            
            Assert.Equal(64, state2.Width);
            Assert.Equal(64, state2.Height);
            Assert.Equal(DXGI_FORMAT.BC7_UNORM_SRGB, state2.Format);
            
            Assert.Equal(0.8811911940574646, hash1.Similarity(hash2));
        }
        
                
        [Fact]
        public async Task CanLoadAndCompareResizedVFlipImage()
        {
            var hash1 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            
            var hash2 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5-small-bc7-vflip.dds"));
            
            Assert.Equal(0.19484494626522064, hash1.Similarity(hash2));
        }
        
        [Fact]
        public async Task CanLoadAndCompareRecompressedImage()
        {
            var hash1 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));

            var hash2 = await ImageState.GetPHash(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5-recompressed.dds"));

            Assert.Equal(0.9298737645149231, hash1.Similarity(hash2));
        }
    }
}
