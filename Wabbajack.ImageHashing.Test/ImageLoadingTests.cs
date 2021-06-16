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
            var file1 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            var hash1 = file1.PerceptionHash();
            
            var file2 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            var hash2 = file2.PerceptionHash();
            
            Assert.Equal(1, hash1.Similarity(hash2));
        }
        
        [Fact]
        public async Task CanLoadAndCompareResizedImage()
        {
            var file1 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            var hash1 = file1.PerceptionHash();
            
            var file2 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5-small-bc7.dds"));
            var hash2 = file2.PerceptionHash();
            
            Assert.Equal(0.956666886806488, hash1.Similarity(hash2));
        }
        
                
        [Fact]
        public async Task CanLoadAndCompareResizedVFlipImage()
        {
            var file1 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            var hash1 = file1.PerceptionHash();
            
            var file2 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5-small-bc7-vflip.dds"));
            var hash2 = file2.PerceptionHash();
            
            Assert.Equal(0.2465425431728363, hash1.Similarity(hash2));
        }
        
        [Fact]
        public async Task CanLoadAndCompareRecompressedImage()
        {
            var file1 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
            var hash1 = file1.PerceptionHash();

            var file2 = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5-recompressed.dds"));
            var hash2 = file2.PerceptionHash();

            Assert.Equal(0.9999724626541138, hash1.Similarity(hash2));
        }
    }
}
