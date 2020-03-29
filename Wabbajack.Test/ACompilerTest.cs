using System;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.LibCefHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public abstract class ACompilerTest : IDisposable
    {
        public ITestOutputHelper TestContext { get; set; }
        protected TestUtils utils { get; set; }

        public ACompilerTest(ITestOutputHelper helper)
        {
            TestContext = helper;
            Helpers.Init();
            Consts.TestMode = true;

            utils = new TestUtils();
            utils.Game = Game.SkyrimSpecialEdition;

            DateTime startTime = DateTime.Now;
            Utils.LogMessages.Subscribe(f => TestContext.WriteLine($"{DateTime.Now - startTime} -  {f.ShortDescription}"));

        }

        public void Dispose()
        {
            utils.Dispose();
        }

        protected async Task<MO2Compiler> ConfigureAndRunCompiler(string profile)
        {
            var compiler = new MO2Compiler(
                mo2Folder: utils.MO2Folder,
                mo2Profile: profile,
                outputFile: OutputFile(profile));
            Assert.True(await compiler.Begin());
            return compiler;
        }

        protected async Task<ModList> CompileAndInstall(string profile)
        {
            var compiler = await ConfigureAndRunCompiler(profile);
            Utils.Log("Finished Compiling");
            await Install(compiler);
            return compiler.ModList;
        }

        private static AbsolutePath OutputFile(string profile)
        {
            return ((RelativePath)profile).RelativeToEntryPoint().WithExtension(Consts.ModListExtension);
        }

        protected async Task Install(MO2Compiler compiler)
        {
            Utils.Log("Loading Modlist");
            var modlist = AInstaller.LoadFromFile(compiler.ModListOutputFile);
            Utils.Log("Constructing Installer");
            var installer = new MO2Installer(
                archive: compiler.ModListOutputFile,
                modList: modlist,
                outputFolder: utils.InstallFolder,
                downloadFolder: utils.DownloadsFolder,
                parameters: CreateDummySystemParameters());
            installer.WarnOnOverwrite = false;
            installer.GameFolder = utils.GameFolder;
            Utils.Log("Starting Install");
            await installer.Begin();
        }

        public static SystemParameters CreateDummySystemParameters()
        {
            return new SystemParameters
            {
                WindowsVersion = new Version("6.2.4.0"),
                ScreenWidth = 1920,
                ScreenHeight = 1080,
                SystemMemorySize = 16 * 1024 * 1040,
                VideoMemorySize = 4 * 1024 * 1024
            };
        }
    }
}
