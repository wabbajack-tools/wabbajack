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

        [Fact]
        public async Task VerifyLogoURLs()
        {
            var modlists = await ModlistMetadata.LoadFromGithub();

            foreach (var modlist in modlists.Select(m => m.Links))
            {
                var logoState = DownloadDispatcher.ResolveArchive(modlist.ImageUri);
                Assert.NotNull(logoState);
                Assert.True(await logoState.Verify(new Archive{Size = 0}), $"{modlist.ImageUri} is not valid");
            }
        }

        public ModlistMetadataTests(ITestOutputHelper output) : base(output)
        {
            
        }
    }
}
