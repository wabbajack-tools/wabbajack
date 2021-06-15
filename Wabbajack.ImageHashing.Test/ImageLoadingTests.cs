using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;

namespace Wabbajack.ImageHashing.Test
{
    public class ImageLoadingTests
    {
        [Fact]
        public async Task CanLoadDDSFiles()
        {
            var file = DDSImage.FromFile(AbsolutePath.EntryPoint.Combine("Resources", "test-dxt5.dds"));
        }
    }
}
