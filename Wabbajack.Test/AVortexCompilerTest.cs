using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    public abstract class AVortexCompilerTest
    {
        public TestContext TestContext { get; set; }
        protected TestUtils utils { get; set; }


        [TestInitialize]
        public void TestInitialize()
        {
            Consts.TestMode = true;

            utils = new TestUtils
            {
                GameName = "darkestdungeon"
            };

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            utils.Dispose();
        }

        protected VortexCompiler ConfigureAndRunCompiler()
        {
            var vortexCompiler = MakeCompiler();
            vortexCompiler.VFS.Reset();
            vortexCompiler.DownloadsFolder = utils.DownloadsFolder;
            vortexCompiler.StagingFolder = utils.InstallFolder;
            Directory.CreateDirectory(utils.InstallFolder);
            Assert.IsTrue(vortexCompiler.Compile());
            return vortexCompiler;
        }

        protected VortexCompiler MakeCompiler()
        {
            VirtualFileSystem.Reconfigure(utils.TestFolder);
            var vortexCompiler = new VortexCompiler(utils.GameName, utils.GameFolder);
            return vortexCompiler;
        }

        protected ModList CompileAndInstall()
        {
            var vortexCompiler = ConfigureAndRunCompiler();
            Install(vortexCompiler);
            return vortexCompiler.ModList;
        }

        protected void Install(VortexCompiler vortexCompiler)
        {
            var modList = Installer.LoadFromFile(vortexCompiler.ModListOutputFile);
            var installer = new Installer(vortexCompiler.ModListOutputFile, modList, utils.InstallFolder)
            {
                DownloadFolder = utils.DownloadsFolder,
                GameFolder = utils.GameFolder,
            };
            installer.Install();
        }
    }
}
