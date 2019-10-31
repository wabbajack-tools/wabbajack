using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    [TestClass]
    public class SanityTests : ACompilerTest
    {
        [TestMethod]
        public void TestDirectMatch()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var test_pex = utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", File.ReadAllBytes(test_pex)}});

            CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
        }

        [TestMethod]
        public void UnmodifiedInlinedFilesArePulledFromArchives()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var ini = utils.AddModFile(mod, @"foo.ini", 10);
            utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/biz.pex", File.ReadAllBytes(ini) } });

            var modlist = CompileAndInstall(profile);
            var directive = modlist.Directives.Where(m => m.To == $"mods\\{mod}\\foo.ini").FirstOrDefault();

            Assert.IsNotNull(directive);
            Assert.IsInstanceOfType(directive, typeof(FromArchive));
        }

        [TestMethod]
        public void ModifiedIniFilesArePatchedAgainstFileWithSameName()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var ini = utils.AddModFile(mod, @"foo.ini", 10);
            utils.Configure();


            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/foo.ini", File.ReadAllBytes(ini) } });

            // Modify after creating mod archive in the downloads folder
            File.WriteAllText(ini, "Wabbajack, Wabbajack, Wabbajack!");

            var modlist = CompileAndInstall(profile);
            var directive = modlist.Directives.Where(m => m.To == $"mods\\{mod}\\foo.ini").FirstOrDefault();

            Assert.IsNotNull(directive);
            Assert.IsInstanceOfType(directive, typeof(PatchedFromArchive));
        }


        private ModList CompileAndInstall(string profile)
        {
            var compiler = ConfigureAndRunCompiler(profile);
            Install(compiler);
            return compiler.ModList;
        }

        private void Install(Compiler compiler)
        {
            var modlist = Installer.LoadFromFile(compiler.ModListOutputFile);
            var installer = new Installer(compiler.ModListOutputFile, modlist, utils.InstallFolder);
            installer.DownloadFolder = utils.DownloadsFolder;
            installer.GameFolder = utils.GameFolder;
            installer.Install();
        }
    }
}
