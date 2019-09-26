using System;
using System.Collections.Generic;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using VFS;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class SanityTests
    {
        public TestContext TestContext { get; set; }

        private TestUtils utils { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            utils = new TestUtils();
            utils.GameName = "Skyrim Special Edition";

            Utils.SetStatusFn((f, idx) => { });
            Utils.SetLoggerFn(f => TestContext.WriteLine(f));
            WorkQueue.Init((x, y, z) => { }, (min, max) => { });

        }

        [TestCleanup]
        public void TestCleanup()
        {
            utils.Dispose();
        }

        [TestMethod]
        public void TestDirectMatch()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var test_pex = utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", File.ReadAllBytes(test_pex)}});


            var compiler = ConfigureCompiler(profile);
            Assert.IsTrue(compiler.Compile());

            Install(compiler);
            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
        }


        private void Install(Compiler compiler)
        {
            var installer = new Installer(compiler.ModList, utils.InstallFolder);
            installer.DownloadFolder = utils.DownloadsFolder;
            installer.GameFolder = utils.GameFolder;
            installer.Install();
        }

        private Compiler ConfigureCompiler(string profile)
        {
            VirtualFileSystem.Reconfigure(utils.TestFolder);
            var compiler = new Compiler(utils.MO2Folder);
            compiler.MO2Profile = profile;
            compiler.ShowReportWhenFinished = false;
            return compiler;
        }
    }
}
