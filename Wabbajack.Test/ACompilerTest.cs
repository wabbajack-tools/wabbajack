using System;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Test
{
    public abstract class  ACompilerTest
    {
        public TestContext TestContext { get; set; }
        protected TestUtils utils { get; set; }

        [TestInitialize]
        public async Task TestInitialize()
        {
            await Helpers.Initialize();
            Consts.TestMode = true;

            utils = new TestUtils();
            utils.Game = Game.SkyrimSpecialEdition;

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f.ShortDescription));

        }

        [TestCleanup]
        public void TestCleanup()
        {
            utils.Dispose();
        }

        protected async Task<MO2Compiler> ConfigureAndRunCompiler(string profile)
        {
            var compiler = new MO2Compiler(
                mo2Folder: utils.MO2Folder,
                mo2Profile: profile,
                outputFile: profile + ExtensionManager.Extension);
            compiler.ModListOutputFolder = Path.Combine(utils.TestFolder, "output_folder");
            compiler.SyncVFS = false;
            compiler.ShowReportWhenFinished = false;
            Assert.IsTrue(await compiler.Begin());
            return compiler;
        }

        protected async Task<ModList> CompileAndInstall(string profile)
        {
            var compiler = await ConfigureAndRunCompiler(profile);
            Install(compiler);
            return compiler.ModList;
        }

        protected void Install(MO2Compiler compiler)
        {
            var modlist = AInstaller.LoadFromFile(compiler.ModListOutputFile);
            var installer = new MO2Installer(
                archive: compiler.ModListOutputFile,
                modList: modlist,
                outputFolder: utils.InstallFolder,
                downloadFolder: utils.DownloadsFolder);
            installer.WarnOnOverwrite = false;
            installer.GameFolder = utils.GameFolder;
            installer.Begin().Wait();
        }
    }
}
