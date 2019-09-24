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

        [TestMethod]
        public void TestDirectMatch()
        {
            var utils = new TestUtils();

            utils.GameName = "Skyrim Special Edition";
            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var test_pex = utils.AddModFile(mod, @"Data\scripts\test.pex", 10);
            utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]>() {{"/baz/biz.pex", File.ReadAllBytes(test_pex)}});

            Utils.SetStatusFn((f, idx) => { });
            Utils.SetLoggerFn(f => TestContext.WriteLine(f));
            WorkQueue.Init((x, y, z) => { }, (min, max) => { });

            VirtualFileSystem.Reconfigure(utils.TestFolder);
            var compiler = new Compiler(utils.MO2Folder, msg => TestContext.WriteLine(msg));
            compiler.ShowReportWhenFinished = false;
            compiler.MO2Profile = profile;
            Assert.IsTrue(compiler.Compile());


            var installer = new Installer(compiler.ModList, utils.InstallFolder, TestContext.WriteLine);
            installer.DownloadFolder = utils.DownloadsFolder;
            installer.GameFolder = utils.GameFolder;
            installer.Install();

            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");

            utils.Dispose();

        }
    }
}
