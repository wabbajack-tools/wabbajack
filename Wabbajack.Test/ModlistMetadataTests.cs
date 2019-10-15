using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.ModListRegistry;

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
    }
}
