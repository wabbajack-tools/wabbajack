using Wabbajack.Common;
using Xunit;

namespace Compression.BSA.Test
{
    public class UnitTests
    {
        [Fact]
        public void HashesRespectFolderExtensions()
        {
            Assert.Equal((ulong)0x085B31F63008E2B6, BSAUtils.GetBSAHash("005930b6.dds"));
            
            
            // Old code has a bug where we were stripping the `.esp` from the folder which we shoudn't do when creating folder paths
            Assert.Equal((ulong)0x38C7A858743A7370, BSAUtils.GetFolderBSAHash((RelativePath)@"textures\actors\character\facegendata\facetint\darkend.esp"));
        }
        
    }
}
