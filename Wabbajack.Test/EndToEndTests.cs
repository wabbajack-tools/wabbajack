using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Util;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Test
{
    [TestClass]
    public class EndToEndTests
    {
        private const string DOWNLOAD_FOLDER = "downloads";

        private TestUtils utils = new TestUtils();

        public TestContext TestContext { get; set; }

        public WorkQueue Queue { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            Queue = new WorkQueue();
            Consts.TestMode = true;

            utils = new TestUtils();
            utils.Game = Game.SkyrimSpecialEdition;

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine($"{DateTime.Now} - {f}"));

            if (!Directory.Exists(DOWNLOAD_FOLDER))
                Directory.CreateDirectory(DOWNLOAD_FOLDER);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Queue.Dispose();
        }

        [TestMethod]
        public async Task CreateModlist()
        {
            var profile = utils.AddProfile("Default");
            var mod = utils.AddMod();

            await DownloadAndInstall(
                "https://github.com/ModOrganizer2/modorganizer/releases/download/v2.2.1/Mod.Organizer.2.2.1.7z",
                "Mod.Organizer.2.2.1.7z",
                utils.MO2Folder);
            File.WriteAllLines(Path.Combine(utils.DownloadsFolder, "Mod.Organizer.2.2.1.7z.meta"),
                new List<string>
                {
                    "[General]",
                    "directURL=https://github.com/ModOrganizer2/modorganizer/releases/download/v2.2.1/Mod.Organizer.2.2.1.7z"
                });

            var modfiles = await Task.WhenAll(
                DownloadAndInstall(Game.SkyrimSpecialEdition, 12604, "SkyUI"), 
                DownloadAndInstall(Game.Fallout4, 11925, "Anti-Tank Rifle"), 
                DownloadAndInstall(Game.SkyrimSpecialEdition, 4783, "Frost Armor UNP"), 
                DownloadAndInstall(Game.SkyrimSpecialEdition, 32359, "Frost Armor HDT"));
            
            // We're going to fully patch this mod from another source.
            File.Delete(modfiles[3].Download);

            utils.Configure();
            
            File.WriteAllLines(Path.Combine(modfiles[3].ModFolder, "meta.ini"), new []
            {
                "[General]",
                $"matchAll= {Path.GetFileName(modfiles[2].Download)}"
            });
            
            File.WriteAllLines(Path.Combine(utils.MO2Folder, "startup.bat"), new []
            {
                "ModOrganizer2.exe SKSE"
            });


            var modlist = await CompileAndInstall(profile);
            utils.VerifyAllFiles();

            var loot_folder = Path.Combine(utils.InstallFolder, "LOOT Config Files");
            if (Directory.Exists(loot_folder))
                Utils.DeleteDirectory(loot_folder);

            var compiler = new MO2Compiler(
                mo2Folder: utils.InstallFolder,
                mo2Profile: profile,
                outputFile: profile + Consts.ModListExtension);
            compiler.MO2DownloadsFolder = Path.Combine(utils.DownloadsFolder);
            Assert.IsTrue(await compiler.Begin());

        }

        private async Task DownloadAndInstall(string url, string filename, string mod_name = null)
        {
            var src = Path.Combine(DOWNLOAD_FOLDER, filename);
            if (!File.Exists(src))
            {
                var state = DownloadDispatcher.ResolveArchive(url);
                await state.Download(new Archive { Name = "Unknown"}, src);
            }

            if (!Directory.Exists(utils.DownloadsFolder))
            {
                Directory.CreateDirectory(utils.DownloadsFolder);
            }

            await Utils.CopyFileAsync(src, Path.Combine(utils.DownloadsFolder, filename));

            await FileExtractor.ExtractAll(Queue, src,
                mod_name == null ? utils.MO2Folder : Path.Combine(utils.ModsFolder, mod_name));
        }

        private async Task<(string Download, string ModFolder)> DownloadAndInstall(Game game, int modid, string mod_name)
        {
            utils.AddMod(mod_name);
            var client = await NexusApiClient.Get();
            var resp = await client.GetModFiles(game, modid);
            var file = resp.files.FirstOrDefault(f => f.is_primary) ?? resp.files.FirstOrDefault(f => !string.IsNullOrEmpty(f.category_name));

            var src = Path.Combine(DOWNLOAD_FOLDER, file.file_name);

            var ini = string.Join("\n",
                new List<string>
                {
                    "[General]",
                    $"gameName={game.MetaData().MO2ArchiveName}",
                    $"modID={modid}",
                    $"fileID={file.file_id}"
                });

            if (!File.Exists(src))
            {

                var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());
                await state.Download(src);
            }

            if (!Directory.Exists(utils.DownloadsFolder))
            {
                Directory.CreateDirectory(utils.DownloadsFolder);
            }

            var dest = Path.Combine(utils.DownloadsFolder, file.file_name);
            await Utils.CopyFileAsync(src, dest);

            var modFolder = Path.Combine(utils.ModsFolder, mod_name);
            await FileExtractor.ExtractAll(Queue, src, modFolder);

            File.WriteAllText(dest + Consts.MetaFileExtension, ini);
            return (dest, modFolder);
        }

        private async Task<ModList> CompileAndInstall(string profile)
        {
            var compiler = await ConfigureAndRunCompiler(profile);
            await Install(compiler);
            return compiler.ModList;
        }

        private async Task Install(MO2Compiler compiler)
        {
            var modlist = AInstaller.LoadFromFile(compiler.ModListOutputFile);
            var installer = new MO2Installer(
                archive: compiler.ModListOutputFile, 
                modList: modlist,
                outputFolder: utils.InstallFolder,
                downloadFolder: utils.DownloadsFolder,
                parameters: SystemParametersConstructor.Create());
            installer.GameFolder = utils.GameFolder;
            await installer.Begin();
        }

        private async Task<MO2Compiler> ConfigureAndRunCompiler(string profile)
        {
            var compiler = new MO2Compiler(
                mo2Folder: utils.MO2Folder,
                mo2Profile: profile,
                outputFile: profile + Consts.ModListExtension);
            Assert.IsTrue(await compiler.Begin());
            return compiler;
        }
    }
}
