using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Test
{
    [TestClass]
    public class ModlistMetadataTests
    {
        [TestMethod]
        public async Task TestLoadingModlists()
        {
            var modlists = await ModlistMetadata.LoadFromGithub();
            Assert.IsTrue(modlists.Count > 0);
        }

        [TestMethod]
        public async Task VerifyLogoURLs()
        {
            var modlists = await ModlistMetadata.LoadFromGithub();

            foreach (var modlist in modlists.Select(m => m.Links))
            {
                var logo_state = DownloadDispatcher.ResolveArchive(modlist.ImageUri);
                Assert.IsNotNull(logo_state);
                Assert.IsTrue(await logo_state.Verify(new Archive{Size = 0}), $"{modlist.ImageUri} is not valid");
            }
        }
    }
}
