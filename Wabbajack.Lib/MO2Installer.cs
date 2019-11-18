using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Wabbajack.VirtualFileSystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class MO2Installer : AInstaller
    {
        public MO2Installer(string archive, ModList mod_list, string output_folder)
        {
            ModManager = ModManager.MO2;
            ModListArchive = archive;
            OutputFolder = output_folder;
            DownloadFolder = Path.Combine(OutputFolder, "downloads");
            ModList = mod_list;
        }

        public string GameFolder { get; set; }

        protected override bool _Begin()
        {
            ConfigureProcessor(10);
            var game = GameRegistry.Games[ModList.GameType];

            if (GameFolder == null)
                GameFolder = game.GameLocation;

            if (GameFolder == null)
            {
                MessageBox.Show(
                    $"In order to do a proper install Wabbajack needs to know where your {game.MO2Name} folder resides. We tried looking the" +
                    "game location up in the windows registry but were unable to find it, please make sure you launch the game once before running this installer. ",
                    "Could not find game location", MessageBoxButton.OK);
                Utils.Log("Exiting because we couldn't find the game folder.");
                return false;
            }

            ValidateGameESMs();
            ValidateModlist.RunValidation(ModList);

            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(DownloadFolder);

            if (Directory.Exists(Path.Combine(OutputFolder, "mods")))
            {
                if (MessageBox.Show(
                        "There already appears to be a Mod Organizer 2 install in this folder, are you sure you wish to continue" +
                        " with installation? If you do, you may render both your existing install and the new modlist inoperable.",
                        "Existing MO2 installation in install folder",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation) == MessageBoxResult.No)
                {
                    Utils.Log("Existing installation at the request of the user, existing mods folder found.");
                    return false;
                }
            }


            HashArchives();
            DownloadArchives();
            HashArchives();

            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            if (missing.Count > 0)
            {
                foreach (var a in missing)
                    Info($"Unable to download {a.Name}");
                if (IgnoreMissingFiles)
                    Info("Missing some archives, but continuing anyways at the request of the user");
                else
                    Error("Cannot continue, was unable to download one or more archives");
            }

            PrimeVFS();

            BuildFolderStructure();
            InstallArchives();
            InstallIncludedFiles();
            InstallIncludedDownloadMetas();
            BuildBSAs();

            zEditIntegration.GenerateMerges(this);

            Info("Installation complete! You may exit the program.");
            // Removed until we decide if we want this functionality
            // Nexus devs weren't sure this was a good idea, I (halgari) agree.
            //AskToEndorse();
            return true;
        }

        private void InstallIncludedDownloadMetas()
        {
            ModList.Directives
                   .OfType<ArchiveMeta>()
                   .PMap(Queue, directive =>
                   {
                       Status($"Writing included .meta file {directive.To}");
                       var out_path = Path.Combine(DownloadFolder, directive.To);
                       if (File.Exists(out_path)) File.Delete(out_path);
                       File.WriteAllBytes(out_path, LoadBytesFromPath(directive.SourceDataID));
                   });
        }

        private void ValidateGameESMs()
        {
            foreach (var esm in ModList.Directives.OfType<CleanedESM>().ToList())
            {
                var filename = Path.GetFileName(esm.To);
                var game_file = Path.Combine(GameFolder, "Data", filename);
                Utils.Log($"Validating {filename}");
                var hash = game_file.FileHash();
                if (hash != esm.SourceESMHash)
                {
                    Utils.Error("Game ESM hash doesn't match, is the ESM already cleaned? Please verify your local game files.");
                }
            }
        }

        private void AskToEndorse()
        {
            var mods = ModList.Archives
                .Select(m => m.State)
                .OfType<NexusDownloader.State>()
                .GroupBy(f => (f.GameName, f.ModID))
                .Select(mod => mod.First())
                .ToArray();

            var result = MessageBox.Show(
                $"Installation has completed, but you have installed {mods.Length} from the Nexus, would you like to" +
                " endorse these mods to show support to the authors? It will only take a few moments.", "Endorse Mods?",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Shuffle mods so that if we hit a API limit we don't always miss the same mods
            var r = new Random();
            for (var i = 0; i < mods.Length; i++)
            {
                var a = r.Next(mods.Length);
                var b = r.Next(mods.Length);
                var tmp = mods[a];
                mods[a] = mods[b];
                mods[b] = tmp;
            }

            mods.PMap(Queue, mod =>
            {
                var er = new NexusApiClient().EndorseMod(mod);
                Utils.Log($"Endorsed {mod.GameName} - {mod.ModID} - Result: {er.message}");
            });
            Info("Done! You may now exit the application!");
        }

        private void BuildBSAs()
        {
            var bsas = ModList.Directives.OfType<CreateBSA>().ToList();
            Info($"Building {bsas.Count} bsa files");

            bsas.Do(bsa =>
            {
                Status($"Building {bsa.To}");
                var source_dir = Path.Combine(OutputFolder, Consts.BSACreationDir, bsa.TempID);

                using (var a = bsa.State.MakeBuilder())
                {
                    bsa.FileStates.PMap(Queue, state =>
                    {
                        Status($"Adding {state.Path} to BSA");
                        using (var fs = File.OpenRead(Path.Combine(source_dir, state.Path)))
                        {
                            a.AddFile(state, fs);
                        }
                    });

                    Info($"Writing {bsa.To}");
                    a.Build(Path.Combine(OutputFolder, bsa.To));
                }
            });


            var bsa_dir = Path.Combine(OutputFolder, Consts.BSACreationDir);
            if (Directory.Exists(bsa_dir))
            {
                Info($"Removing temp folder {Consts.BSACreationDir}");
                Directory.Delete(bsa_dir, true, true);
            }
        }

        private void InstallIncludedFiles()
        {
            Info("Writing inline files");
            ModList.Directives
                .OfType<InlineFile>()
                .PMap(Queue, directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var out_path = Path.Combine(OutputFolder, directive.To);
                    if (File.Exists(out_path)) File.Delete(out_path);
                    if (directive is RemappedInlineFile)
                        WriteRemappedFile((RemappedInlineFile)directive);
                    else if (directive is CleanedESM)
                        GenerateCleanedESM((CleanedESM)directive);
                    else
                        File.WriteAllBytes(out_path, LoadBytesFromPath(directive.SourceDataID));
                });
        }

        private void GenerateCleanedESM(CleanedESM directive)
        {
            var filename = Path.GetFileName(directive.To);
            var game_file = Path.Combine(GameFolder, "Data", filename);
            Info($"Generating cleaned ESM for {filename}");
            if (!File.Exists(game_file)) throw new InvalidDataException($"Missing {filename} at {game_file}");
            Status($"Hashing game version of {filename}");
            var sha = game_file.FileHash();
            if (sha != directive.SourceESMHash)
                throw new InvalidDataException(
                    $"Cannot patch {filename} from the game folder hashes don't match have you already cleaned the file?");

            var patch_data = LoadBytesFromPath(directive.SourceDataID);
            var to_file = Path.Combine(OutputFolder, directive.To);
            Status($"Patching {filename}");
            using (var output = File.OpenWrite(to_file))
            using (var input = File.OpenRead(game_file))
            {
                BSDiff.Apply(input, () => new MemoryStream(patch_data), output);
            }
        }

        private void WriteRemappedFile(RemappedInlineFile directive)
        {
            var data = Encoding.UTF8.GetString(LoadBytesFromPath(directive.SourceDataID));

            data = data.Replace(Consts.GAME_PATH_MAGIC_BACK, GameFolder);
            data = data.Replace(Consts.GAME_PATH_MAGIC_DOUBLE_BACK, GameFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.GAME_PATH_MAGIC_FORWARD, GameFolder.Replace("\\", "/"));

            data = data.Replace(Consts.MO2_PATH_MAGIC_BACK, OutputFolder);
            data = data.Replace(Consts.MO2_PATH_MAGIC_DOUBLE_BACK, OutputFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.MO2_PATH_MAGIC_FORWARD, OutputFolder.Replace("\\", "/"));

            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_BACK, DownloadFolder);
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK, DownloadFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_FORWARD, DownloadFolder.Replace("\\", "/"));

            File.WriteAllText(Path.Combine(OutputFolder, directive.To), data);
        }
    }
}
