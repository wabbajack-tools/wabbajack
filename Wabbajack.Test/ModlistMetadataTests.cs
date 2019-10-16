using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            foreach (var modlist in modlists)
            {
                var logo_state = DownloadDispatcher.ResolveArchive(modlist.LogoUrl);
                Assert.IsNotNull(logo_state);
                Assert.IsTrue(logo_state.Verify(), $"{modlist.LogoUrl} is not valid");

                modlist.LoadLogo();
                Assert.IsNotNull(modlist.Logo);
            }
        }
    }
}
