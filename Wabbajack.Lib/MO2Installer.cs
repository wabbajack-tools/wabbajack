using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using Alphaleonis.Win32.Filesystem;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps.CompilationErrors;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using SectionData = Wabbajack.Common.SectionData;

namespace Wabbajack.Lib
{
    public class MO2Installer : AInstaller
    {
        public bool WarnOnOverwrite { get; set; } = true;

        public override ModManager ModManager => ModManager.MO2;

        public AbsolutePath? GameFolder { get; set; }

        public MO2Installer(AbsolutePath archive, ModList modList, AbsolutePath outputFolder, AbsolutePath downloadFolder, SystemParameters parameters)
            : base(
                  archive: archive,
                  modList: modList,
                  outputFolder: outputFolder, 
                  downloadFolder: downloadFolder,
                  parameters: parameters)
        {
        }

        protected override async Task<bool> _Begin(CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return false;
            var metric = Metrics.Send(Metrics.BeginInstall, ModList.Name);

            ConfigureProcessor(20, ConstructDynamicNumThreads(await RecommendQueueSize()));
            var game = ModList.GameType.MetaData();

            if (GameFolder == null)
                GameFolder = game.GameLocation();

            if (GameFolder == null)
            {
                var otherGame = game.CommonlyConfusedWith.Where(g => g.MetaData().IsInstalled).Select(g => g.MetaData()).FirstOrDefault();
                if (otherGame != null)
                {
                    await Utils.Log(new CriticalFailureIntervention(
                            $"In order to do a proper install Wabbajack needs to know where your {game.HumanFriendlyGameName} folder resides. However this game doesn't seem to be installed, we did however find a installed " +
                            $"copy of {otherGame.HumanFriendlyGameName}, did you install the wrong game?",
                            $"Could not locate {game.HumanFriendlyGameName}"))
                        .Task;
                }
                else
                {
                    await Utils.Log(new CriticalFailureIntervention(
                            $"In order to do a proper install Wabbajack needs to know where your {game.HumanFriendlyGameName} folder resides. However this game doesn't seem to be installed",
                            $"Could not locate {game.HumanFriendlyGameName}"))
                        .Task;
                }

                Utils.Log("Exiting because we couldn't find the game folder.");
                return false;
            }

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Validating Game ESMs");
            ValidateGameESMs();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Validating Modlist");
            await ValidateModlist.RunValidation(ModList);

            OutputFolder.CreateDirectory();
            DownloadFolder.CreateDirectory();

            if (OutputFolder.Combine(Consts.MO2ModFolderName).IsDirectory && WarnOnOverwrite)
            {
                if ((await Utils.Log(new ConfirmUpdateOfExistingInstall { ModListName = ModList.Name, OutputFolder = OutputFolder }).Task) == ConfirmUpdateOfExistingInstall.Choice.Abort)
                {
                    Utils.Log("Exiting installation at the request of the user, existing mods folder found.");
                    return false;
                }
            }

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Optimizing ModList");
            await OptimizeModlist();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Archives");
            await HashArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Downloading Missing Archives");
            await DownloadArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Remaining Archives");
            await HashArchives();

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

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Priming VFS");
            await PrimeVFS();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Building Folder Structure");
            BuildFolderStructure();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Archives");
            await InstallArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Included files");
            await InstallIncludedFiles();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Archive Metas");
            await InstallIncludedDownloadMetas();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Building BSAs");
            await BuildBSAs();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Generating Merges");
            await zEditIntegration.GenerateMerges(this);

            UpdateTracker.NextStep("Set MO2 into portable");
            await ForcePortable();

            UpdateTracker.NextStep("Create Empty Output Mods");
            CreateOutputMods();

            UpdateTracker.NextStep("Updating System-specific ini settings");
            SetScreenSizeInPrefs();

            UpdateTracker.NextStep("Installation complete! You may exit the program.");
            var metric2 = Metrics.Send(Metrics.FinishInstall, ModList.Name);

            return true;
        }

        private void CreateOutputMods()
        {
            
            OutputFolder.Combine("profiles")
                .EnumerateFiles(true)
                .Where(f => f.FileName == Consts.SettingsIni)
                .Do(f =>
            {
                var ini = f.LoadIniFile();
                if (ini == null)
                {
                    Utils.Log($"settings.ini is null for {f}, skipping");
                    return;
                }

                var overwrites = ini.custom_overwrites;
                if (overwrites == null)
                {
                    Utils.Log("No custom overwrites found, skipping");
                    return;
                }

                if (overwrites is SectionData data)
                {
                    data.Coll.Do(keyData =>
                    {
                        var v = keyData.Value;
                        var mod = OutputFolder.Combine(Consts.MO2ModFolderName, (RelativePath)v);

                        mod.CreateDirectory();
                    });
                }
            });
        }

        private async Task ForcePortable()
        {
            var path = OutputFolder.Combine("portable.txt");
            if (path.Exists) return;

            try
            {
                await path.WriteAllTextAsync("Created by Wabbajack");
            }
            catch (Exception e)
            {
                Utils.Error(e, $"Could not create portable.txt in {OutputFolder}");
            }
        }

        private async Task InstallIncludedDownloadMetas()
        {
            await ModList.Directives
                   .OfType<ArchiveMeta>()
                   .PMap(Queue, async directive =>
                   {
                       Status($"Writing included .meta file {directive.To}");
                       var outPath = DownloadFolder.Combine(directive.To);
                       if (outPath.IsFile) outPath.Delete();
                       await outPath.WriteAllBytesAsync(await LoadBytesFromPath(directive.SourceDataID));
                   });
        }

        private void ValidateGameESMs()
        {
            foreach (var esm in ModList.Directives.OfType<CleanedESM>().ToList())
            {
                var filename = esm.To.FileName;
                var gameFile = GameFolder.Value.Combine((RelativePath)"Data", filename);
                Utils.Log($"Validating {filename}");
                var hash = gameFile.FileHash();
                if (hash != esm.SourceESMHash)
                {
                    Utils.ErrorThrow(new InvalidGameESMError(esm, hash, gameFile));
                }
            }
        }

        private async Task BuildBSAs()
        {
            var bsas = ModList.Directives.OfType<CreateBSA>().ToList();
            Info($"Building {bsas.Count} bsa files");

            foreach (var bsa in bsas)
            {
                Status($"Building {bsa.To}");
                var sourceDir = OutputFolder.Combine(Consts.BSACreationDir, bsa.TempID);

                var bsaSize = bsa.FileStates.Select(state => sourceDir.Combine(state.Path).Size).Sum();

                using (var a = bsa.State.MakeBuilder(bsaSize))
                {
                    var streams = await bsa.FileStates.PMap(Queue, state =>
                    {
                        Status($"Adding {state.Path} to BSA");
                        var fs = sourceDir.Combine(state.Path).OpenRead();
                        a.AddFile(state, fs);
                        return fs;
                    });

                    Info($"Writing {bsa.To}");
                    a.Build(OutputFolder.Combine(bsa.To));
                    streams.Do(s => s.Dispose());
                }
            }

            var bsaDir = OutputFolder.Combine(Consts.BSACreationDir);
            if (bsaDir.Exists)
            {
                Info($"Removing temp folder {Consts.BSACreationDir}");
                Utils.DeleteDirectory(bsaDir);
            }
        }

        private async Task InstallIncludedFiles()
        {
            Info("Writing inline files");
            await ModList.Directives
                .OfType<InlineFile>()
                .PMap(Queue, async directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var outPath = OutputFolder.Combine(directive.To);
                    outPath.Delete();

                    switch (directive)
                    {
                        case RemappedInlineFile file:
                            await WriteRemappedFile(file);
                            break;
                        case CleanedESM esm:
                            await GenerateCleanedESM(esm);
                            break;
                        default:
                            await outPath.WriteAllBytesAsync(await LoadBytesFromPath(directive.SourceDataID));
                            break;
                    }
                });
        }

        private async Task GenerateCleanedESM(CleanedESM directive)
        {
            var filename = directive.To.FileName;
            var gameFile = GameFolder.Value.Combine((RelativePath)"Data", filename);
            Info($"Generating cleaned ESM for {filename}");
            if (!gameFile.Exists) throw new InvalidDataException($"Missing {filename} at {gameFile}");
            Status($"Hashing game version of {filename}");
            var sha = await gameFile.FileHashAsync();
            if (sha != directive.SourceESMHash)
                throw new InvalidDataException(
                    $"Cannot patch {filename} from the game folder because the hashes do not match. Have you already cleaned the file?");

            var patchData = await LoadBytesFromPath(directive.SourceDataID);
            var toFile = OutputFolder.Combine(directive.To);
            Status($"Patching {filename}");
            using var output = toFile.Create();
            using var input = gameFile.OpenRead();
            Utils.ApplyPatch(input, () => new MemoryStream(patchData), output);
        }

        private void SetScreenSizeInPrefs()
        {
            var config = new IniParserConfiguration {AllowDuplicateKeys = true, AllowDuplicateSections = true};
            foreach (var file in OutputFolder.Combine("profiles").EnumerateFiles()
                .Where(f => ((string)f.FileName).EndsWith("*refs.ini")))
            {
                try
                {
                    var parser = new FileIniDataParser(new IniDataParser(config));
                    var data = parser.ReadFile((string)file);
                    bool modified = false;
                    if (data.Sections["Display"] != null)
                    {

                        if (data.Sections["Display"]["iSize W"] != null && data.Sections["Display"]["iSize H"] != null)
                        {
                            data.Sections["Display"]["iSize W"] =
                                SystemParameters.ScreenWidth.ToString(CultureInfo.CurrentCulture);
                            data.Sections["Display"]["iSize H"] =
                                SystemParameters.ScreenHeight.ToString(CultureInfo.CurrentCulture);
                            modified = true;
                        }

                    }
                    if (data.Sections["MEMORY"] != null)
                    {
                        if (data.Sections["MEMORY"]["VideoMemorySizeMb"] != null)
                        {
                            data.Sections["MEMORY"]["VideoMemorySizeMb"] =
                                SystemParameters.EnbLEVRAMSize.ToString(CultureInfo.CurrentCulture);
                            modified = true;
                        }
                    }

                    if (modified) 
                        parser.WriteFile((string)file, data);
                }
                catch (Exception ex)
                {
                    Utils.Log($"Skipping screen size remap for {file} due to parse error.");
                    continue;
                }
            }
        }

        private async Task WriteRemappedFile(RemappedInlineFile directive)
        {
            var data = Encoding.UTF8.GetString(await LoadBytesFromPath(directive.SourceDataID));

            data = data.Replace(Consts.GAME_PATH_MAGIC_BACK, (string)GameFolder);
            data = data.Replace(Consts.GAME_PATH_MAGIC_DOUBLE_BACK, ((string)GameFolder).Replace("\\", "\\\\"));
            data = data.Replace(Consts.GAME_PATH_MAGIC_FORWARD, ((string)GameFolder).Replace("\\", "/"));

            data = data.Replace(Consts.MO2_PATH_MAGIC_BACK, (string)OutputFolder);
            data = data.Replace(Consts.MO2_PATH_MAGIC_DOUBLE_BACK, ((string)OutputFolder).Replace("\\", "\\\\"));
            data = data.Replace(Consts.MO2_PATH_MAGIC_FORWARD, ((string)OutputFolder).Replace("\\", "/"));

            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_BACK, (string)DownloadFolder);
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK, ((string)DownloadFolder).Replace("\\", "\\\\"));
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_FORWARD, ((string)DownloadFolder).Replace("\\", "/"));

            await OutputFolder.Combine(directive.To).WriteAllTextAsync(data);
        }

        public static IErrorResponse CheckValidInstallPath(AbsolutePath path, AbsolutePath? downloadFolder)
        {
            if (!path.Exists) return ErrorResponse.Success;

            // Check folder does not have a Wabbajack ModList
            if (path.EnumerateFiles(false).Where(file => file.Exists).Any(file => file.Extension == Consts.ModListExtension))
            {
                return ErrorResponse.Fail($"Cannot install into a folder with a Wabbajack ModList inside of it");
            }

            // Check folder is either empty, or a likely valid previous install
            if (path.IsEmptyDirectory)
            {
                return ErrorResponse.Success;
            }

            // If we have a MO2 install, assume good to go
            if (path.EnumerateFiles(false).Any(file =>
            {
                if (file.FileName == Consts.ModOrganizer2Exe) return true;
                if (file.FileName == Consts.ModOrganizer2Ini) return true;
                return false;
            }))
            {
                return ErrorResponse.Success;
            }

            // If we don't have a MO2 install, and there's any file that's not in the downloads folder, mark failure
            if (downloadFolder.HasValue && path.EnumerateFiles(true).All(file => file.InFolder(downloadFolder.Value)))
            {
                return ErrorResponse.Success;
            }

            return ErrorResponse.Fail($"Cannot install to this folder as it has unknown files that could be deleted");
        }
    }
}
