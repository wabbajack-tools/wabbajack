using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    [TestClass]
    public class zEditIntegrationTests : ACompilerTest
    {
        [TestMethod]
        public async Task CanCreatezEditPatches()
        {
            var profile = utils.AddProfile();
            var moda = utils.AddMod();
            var modb = utils.AddMod();
            var moddest = utils.AddMod();
            var srca = utils.AddModFile(moda, @"srca.esp", 10);
            var srcb = utils.AddModFile(moda, @"srcb.esp.mohidden", 10);
            var srcc = utils.AddModFile(modb, @"optional\srcc.esp", 10);
            var dest = utils.AddModFile(moddest, @"merged.esp", 20);

            var srcs = new List<string> {srca, srcb, srcc};


            Directory.CreateDirectory(Path.Combine(utils.MO2Folder, "tools", "mator", "bleh", "profiles", "myprofile"));

            var settings = new zEditIntegration.zEditSettings()
            {
                modManager = "Mod Organizer 2",
                managerPath = utils.MO2Folder,
                modsPath = Path.Combine(utils.MO2Folder, Consts.MO2ModFolderName),
                mergePath = Path.Combine(utils.MO2Folder, Consts.MO2ModFolderName)
            };

            settings.ToJson(Path.Combine(utils.MO2Folder, "tools", "mator", "bleh", "profiles", "myprofile",
                "settings.json"));

            new List<zEditIntegration.zEditMerge>()
            {
                new zEditIntegration.zEditMerge()
                {
                    name = moddest,
                    filename = "merged.esp",
                    plugins = new List<zEditIntegration.zEditMergePlugin>()
                    {
                        new zEditIntegration.zEditMergePlugin()
                        {
                            filename = "srca.esp",
                            dataFolder = Path.Combine(utils.MO2Folder, Consts.MO2ModFolderName, moda)
                        },
                        new zEditIntegration.zEditMergePlugin()
                        {
                            filename = "srcb.esp",
                            dataFolder = Path.Combine(utils.MO2Folder, Consts.MO2ModFolderName, moda),
                        },
                        new zEditIntegration.zEditMergePlugin()
                        {
                            filename = "srcc.esp",
                            dataFolder = Path.Combine(utils.MO2Folder, Consts.MO2ModFolderName, modb),
                        }
                    }
                }
            }.ToJson(Path.Combine(utils.MO2Folder, "tools", "mator", "bleh", "profiles", "myprofile", "merges.json"));

            utils.Configure();


            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "srca.esp", File.ReadAllBytes(srca) } });
            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "srcb.esp", File.ReadAllBytes(srcb) } });
            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "srcc.esp", File.ReadAllBytes(srcc) } });

            File.AppendAllLines(Path.Combine(utils.MO2Folder, "ModOrganizer.ini"),
                new List<string>
                {
                    "[customExecutables]",
                    "size=1",
                    $@"1\binary={utils.MO2Folder.Replace('\\','/')}/tools/mator/bleh/zEdit.exe"

                });


            
            var modlist = await CompileAndInstall(profile);
            var directive = modlist.Directives.Where(m => m.To == $"mods\\{moddest}\\merged.esp").FirstOrDefault();

            Assert.IsNotNull(directive);
            Assert.IsInstanceOfType(directive, typeof(MergedPatch));

            var merged = directive as MergedPatch;

            foreach (var (source, path) in merged.Sources.Zip(srcs, (a, b) => (a, b)))
            {
                Assert.AreEqual(source.Hash, Utils.FileHash(path));
            }

            utils.VerifyInstalledFile(moddest, "merged.esp");

        }
    }
}
