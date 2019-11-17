using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Wabbajack.Common;
using Wabbajack.Lib;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

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
        public void TestDuplicateFilesAreCopied()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var test_pex = utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            // Make a copy to make sure it gets picked up and moved around.
            File.Copy(test_pex, test_pex + ".copy");

            utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/biz.pex", File.ReadAllBytes(test_pex) } });

            CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex.copy");
        }


        [TestMethod]
        public void CleanedESMTest()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod("Cleaned ESMs");
            var update_esm = utils.AddModFile(mod, @"Update.esm", 10);

            utils.Configure();

            var game_file = Path.Combine(utils.GameFolder, "Data", "Update.esm");
            utils.GenerateRandomFileData(game_file, 20);

            var modlist = CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Update.esm");

            var compiler = ConfigureAndRunCompiler(profile);

            // Update the file and verify that it throws an error.
            utils.GenerateRandomFileData(game_file, 20);
            var exception = Assert.ThrowsException<AggregateException>(() => Install(compiler));
            Assert.AreEqual(exception.InnerExceptions.First().Message, "Game ESM hash doesn't match, is the ESM already cleaned? Please verify your local game files.");


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

    }
}
