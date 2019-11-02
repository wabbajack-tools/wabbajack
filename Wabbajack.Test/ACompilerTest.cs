using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFS;
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

        protected Compiler ConfigureAndRunCompiler(string profile)
        {
            var compiler = MakeCompiler();
            compiler.VFS.Reset();
            compiler.MO2Profile = profile;
            compiler.ShowReportWhenFinished = false;
            Assert.IsTrue(compiler.Compile());
            return compiler;
        }

        protected Compiler MakeCompiler()
        {
            VirtualFileSystem.Reconfigure(utils.TestFolder);
            var compiler = new Compiler(utils.MO2Folder);
            return compiler;
        }
        protected ModList CompileAndInstall(string profile)
        {
            var compiler = ConfigureAndRunCompiler(profile);
            Install(compiler);
            return compiler.ModList;
        }

        protected void Install(Compiler compiler)
        {
            var modlist = Installer.LoadFromFile(compiler.ModListOutputFile);
            var installer = new Installer(compiler.ModListOutputFile, modlist, utils.InstallFolder);
            installer.DownloadFolder = utils.DownloadsFolder;
            installer.GameFolder = utils.GameFolder;
            installer.Install();
        }
    }
}
