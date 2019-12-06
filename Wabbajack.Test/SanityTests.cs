using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        public void TestUpdating()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var unchanged = utils.AddModFile(mod, @"Data\scripts\unchanged.pex", 10);
            var deleted = utils.AddModFile(mod, @"Data\scripts\deleted.pex", 10);
            var modified = utils.AddModFile(mod, @"Data\scripts\modified.pex", 10);

            utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]>
                {
                    { "/baz/unchanged.pex", File.ReadAllBytes(unchanged) },
                    { "/baz/deleted.pex", File.ReadAllBytes(deleted) },
                    { "/baz/modified.pex", File.ReadAllBytes(modified) },
                });

            CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\unchanged.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\deleted.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\modified.pex");

            var unchanged_path = utils.PathOfInstalledFile(mod, @"Data\scripts\unchanged.pex");
            var deleted_path = utils.PathOfInstalledFile(mod, @"Data\scripts\deleted.pex");
            var modified_path = utils.PathOfInstalledFile(mod, @"Data\scripts\modified.pex");

            var extra_path = utils.PathOfInstalledFile(mod, @"something_i_made.foo");
            File.WriteAllText(extra_path, "bleh");


            var unchanged_modified = File.GetLastWriteTime(unchanged_path);
            var modified_modified = File.GetLastWriteTime(modified_path);

            File.WriteAllText(modified_path, "random data");
            File.Delete(deleted_path);

            Assert.IsTrue(File.Exists(extra_path));

            CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\unchanged.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\deleted.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\modified.pex");

            Assert.AreEqual(unchanged_modified, File.GetLastWriteTime(unchanged_path));
            Assert.AreNotEqual(modified_modified, File.GetLastWriteTime(modified_path));
            Assert.IsFalse(File.Exists(extra_path));
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
        public void SetScreenSizeTest()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod("dummy");

            utils.Configure();
            File.WriteAllLines(Path.Combine(utils.MO2Folder, "profiles", profile, "somegameprefs.ini"),
                new List<string>
                {
                    "[Display]",
                    "iSize H=3", 
                    "iSize W=-200"
                });

            var modlist = CompileAndInstall(profile);

            var ini = Path.Combine(utils.InstallFolder, "profiles", profile, "somegameprefs.ini").LoadIniFile();

            Assert.AreEqual(System.Windows.SystemParameters.PrimaryScreenHeight.ToString(), ini?.Display?["iSize H"]);
            Assert.AreEqual(System.Windows.SystemParameters.PrimaryScreenWidth.ToString(), ini?.Display?["iSize W"]);
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
            var meta = utils.AddModFile(mod, "meta.ini");

            utils.Configure();


            var archive = utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/foo.ini", File.ReadAllBytes(ini) } });

            File.WriteAllLines(meta, new[]
            {
                "[General]",
                $"installationFile={archive}",
            });

            // Modify after creating mod archive in the downloads folder
            File.WriteAllText(ini, "Wabbajack, Wabbajack, Wabbajack!");

            var modlist = CompileAndInstall(profile);
            var directive = modlist.Directives.Where(m => m.To == $"mods\\{mod}\\foo.ini").FirstOrDefault();

            Assert.IsNotNull(directive);
            Assert.IsInstanceOfType(directive, typeof(PatchedFromArchive));
        }

    }
}
