using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Test
{
    [TestClass]
    public class ModlistMetadataTests
    {
        [TestMethod]
        public void TestLoadingModlists()
        {
            var modlists = ModlistMetadata.LoadFromGithub();
            Assert.IsTrue(modlists.Count > 0);
        }

        [TestMethod]
        public void VerifyLogoURLs()
        {
            var modlists = ModlistMetadata.LoadFromGithub();

            foreach (var modlist in modlists.Select(m => m.Links))
            {
                var logo_state = DownloadDispatcher.ResolveArchive(modlist.ImageUri);
                Assert.IsNotNull(logo_state);
                Assert.IsTrue(logo_state.Verify(), $"{modlist.ImageUri} is not valid");
            }
        }
    }
}
