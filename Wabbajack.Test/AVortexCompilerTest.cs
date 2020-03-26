using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                Game = Game.DarkestDungeon
            };

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f.ShortDescription));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            utils.Dispose();
        }

        protected async Task<VortexCompiler> ConfigureAndRunCompiler()
        {
            var vortexCompiler = MakeCompiler();
            vortexCompiler.DownloadsFolder = utils.DownloadsFolder;
            vortexCompiler.StagingFolder = utils.InstallFolder;
            Directory.CreateDirectory(utils.InstallFolder);
            Assert.IsTrue(await vortexCompiler.Begin());
            return vortexCompiler;
        }

        protected VortexCompiler MakeCompiler()
        {
            return new VortexCompiler(
                game: utils.Game,
                gamePath: utils.GameFolder,
                vortexFolder: VortexCompiler.TypicalVortexFolder(),
                downloadsFolder: VortexCompiler.RetrieveDownloadLocation(utils.Game),
                stagingFolder: VortexCompiler.RetrieveStagingLocation(utils.Game),
                outputFile: $"test{Consts.ModListExtension}");
        }

        protected async Task<ModList> CompileAndInstall()
        {
            var vortexCompiler = await ConfigureAndRunCompiler();
            await Install(vortexCompiler);
            return vortexCompiler.ModList;
        }

        protected async Task Install(VortexCompiler vortexCompiler)
        {
            var modList = AInstaller.LoadFromFile(vortexCompiler.ModListOutputFile);
            var installer = new MO2Installer(
                archive: vortexCompiler.ModListOutputFile, 
                modList: modList,
                outputFolder: utils.InstallFolder,
                downloadFolder: utils.DownloadsFolder,
                parameters: SystemParametersConstructor.Create())
            {
                GameFolder = utils.GameFolder,
            };
            await installer.Begin();
        }
    }
}
