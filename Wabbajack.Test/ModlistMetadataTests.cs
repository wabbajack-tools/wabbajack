using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public class ModlistMetadataTests : ATestBase
    {
        [Fact]
        public async Task TestLoadingModlists()
        {
            var modlists = await ModlistMetadata.LoadFromGithub();
            Assert.True(modlists.Count > 0);
        }
        public ModlistMetadataTests(ITestOutputHelper output) : base(output)
        {
            
        }
    }
}
