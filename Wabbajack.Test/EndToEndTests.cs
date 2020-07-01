using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.VirtualFileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public class EndToEndTests : XunitContextBase, IDisposable
    {
        private AbsolutePath _downloadFolder = "downloads".RelativeTo(AbsolutePath.EntryPoint);

        private TestUtils utils = new TestUtils();
        private IDisposable _unsub;

        public WorkQueue Queue { get; set; }

        public EndToEndTests(ITestOutputHelper helper) : base(helper)
        {
            Queue = new WorkQueue();
            Consts.TestMode = true;

            utils = new TestUtils();
            utils.Game = Game.SkyrimSpecialEdition;

            _unsub = Utils.LogMessages.Subscribe(f => XunitContext.WriteLine($"{DateTime.Now} - {f}"));
            
            _downloadFolder.CreateDirectory();
        }

        public override void Dispose()
        {
            Queue.Dispose();
            _unsub.Dispose();
            utils.DisposeAsync().AsTask().Wait();
            base.Dispose();
        }

        [Fact]
        public async Task CreateModlist()
        {
            var profile = utils.AddProfile("Default");
            var mod = await utils.AddMod();

            await DownloadAndInstall(
                "https://github.com/ModOrganizer2/modorganizer/releases/download/v2.2.1/Mod.Organizer.2.2.1.7z",
                "Mod.Organizer.2.2.1.7z");
            await utils.DownloadsFolder.Combine("Mod.Organizer.2.2.1.7z.meta").WriteAllLinesAsync(
                    "[General]",
                    "directURL=https://github.com/ModOrganizer2/modorganizer/releases/download/v2.2.1/Mod.Organizer.2.2.1.7z"
                );

            var modfiles = await Task.WhenAll(
                DownloadAndInstall(Game.SkyrimSpecialEdition, 12604, "SkyUI"), 
                DownloadAndInstall(Game.Fallout4, 11925, "Anti-Tank Rifle"), 
                DownloadAndInstall(Game.SkyrimSpecialEdition, 4783, "Frost Armor UNP"), 
                DownloadAndInstall(Game.SkyrimSpecialEdition, 32359, "Frost Armor HDT"));
            
            // We're going to fully patch this mod from another source.
            await modfiles[3].Download.DeleteAsync();

            await utils.Configure();
            
            await modfiles[3].ModFolder.Combine("meta.ini").WriteAllLinesAsync(
                "[General]",
                $"matchAll= {modfiles[2].Download.FileName}"
            );
            
            await utils.MO2Folder.Combine("startup.bat").WriteAllLinesAsync(
                "ModOrganizer2.exe SKSE"
            );


            await CompileAndInstall(profile);
            await utils.VerifyAllFiles();

            await utils.InstallFolder.Combine(Consts.LOOTFolderFilesDir).DeleteDirectory();

            var compiler = new MO2Compiler(
                mo2Folder: utils.InstallFolder,
                mo2Profile: profile,
                outputFile: profile.RelativeTo(AbsolutePath.EntryPoint).WithExtension(Consts.ModListExtension));
            compiler.MO2DownloadsFolder = utils.DownloadsFolder;
            Assert.True(await compiler.Begin());

        }

        private async Task DownloadAndInstall(string url, string filename, string modName = null)
        {
            var src = _downloadFolder.Combine(filename);
            if (!src.Exists)
            {
                var state = DownloadDispatcher.ResolveArchive(url);
                await state.Download(new Archive(state: null!) { Name = "Unknown"}, src);
            }

            utils.DownloadsFolder.CreateDirectory();

            await src.CopyToAsync(utils.DownloadsFolder.Combine(filename));

            await using var dest = await FileExtractor.ExtractAll(Queue, src);
            await dest.MoveAllTo(modName == null ? utils.MO2Folder : utils.ModsFolder.Combine(modName));
        }

        private async Task<(AbsolutePath Download, AbsolutePath ModFolder)> DownloadAndInstall(Game game, int modId, string modName)
        {
            await utils.AddMod(modName);
            var client = await NexusApiClient.Get();
            var resp = await client.GetModFiles(game, modId);
            var file = resp.files.FirstOrDefault(f => f.is_primary) ?? resp.files.FirstOrDefault(f => !string.IsNullOrEmpty(f.category_name));

            var src = _downloadFolder.Combine(file.file_name);

            var ini = string.Join("\n",
                new List<string>
                {
                    "[General]",
                    $"gameName={game.MetaData().MO2ArchiveName}",
                    $"modID={modId}",
                    $"fileID={file.file_id}"
                });

            if (!src.Exists)
            {

                var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());
                await state.Download(src);
            }
            
            utils.DownloadsFolder.CreateDirectory();

            var dest = utils.DownloadsFolder.Combine(file.file_name);
            await src.CopyToAsync(dest);

            var modFolder = utils.ModsFolder.Combine(modName);
            await using var files = await FileExtractor.ExtractAll(Queue, src);
            await files.MoveAllTo(modFolder);

            await dest.WithExtension(Consts.MetaFileExtension).WriteAllTextAsync(ini);
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
                parameters: ACompilerTest.CreateDummySystemParameters());
            installer.GameFolder = utils.GameFolder;
            await installer.Begin();
        }

        private async Task<MO2Compiler> ConfigureAndRunCompiler(string profile)
        {
            var compiler = new MO2Compiler(
                mo2Folder: utils.MO2Folder,
                mo2Profile: profile,
                outputFile: profile.RelativeTo(AbsolutePath.EntryPoint).WithExtension(Consts.ModListExtension));
            Assert.True(await compiler.Begin());
            return compiler;
        }
    }
}
