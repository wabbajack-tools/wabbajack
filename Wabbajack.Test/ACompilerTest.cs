using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    public abstract class  ACompilerTest
    {
        public TestContext TestContext { get; set; }
        protected TestUtils utils { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            Consts.TestMode = true;

            utils = new TestUtils();
            utils.Game = Game.SkyrimSpecialEdition;

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f));

        }

        [TestCleanup]
        public void TestCleanup()
        {
            utils.Dispose();
        }

        protected MO2Compiler ConfigureAndRunCompiler(string profile)
        {
            var compiler = MakeCompiler();
            compiler.MO2Profile = profile;
            compiler.ShowReportWhenFinished = false;
            Assert.IsTrue(compiler.Begin().Result);
            return compiler;
        }

        protected MO2Compiler MakeCompiler()
        {
            var compiler = new MO2Compiler(utils.MO2Folder);
            return compiler;
        }
        protected ModList CompileAndInstall(string profile)
        {
            var compiler = ConfigureAndRunCompiler(profile);
            Install(compiler);
            return compiler.ModList;
        }

        protected void Install(MO2Compiler compiler)
        {
            var modlist = MO2Installer.LoadFromFile(compiler.ModListOutputFile);
            var installer = new MO2Installer(compiler.ModListOutputFile, modlist, utils.InstallFolder);
            installer.WarnOnOverwrite = false;
            installer.DownloadFolder = utils.DownloadsFolder;
            installer.GameFolder = utils.GameFolder;
            installer.Begin().Wait();
        }
    }
}
