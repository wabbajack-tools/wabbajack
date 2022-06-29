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
using System.Collections.Generic;
using Wabbajack.Common.IO;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public class MO2Installer : AInstaller
    {
        public bool WarnOnOverwrite { get; set; } = true;

        public override ModManager ModManager => ModManager.MO2;

        public AbsolutePath? GameFolder { get; set; }
        
        public ModlistMetadata? Metadata { get; set; }

        public MO2Installer(AbsolutePath archive, ModList modList, AbsolutePath outputFolder, AbsolutePath downloadFolder, SystemParameters parameters)
            : base(
                  archive: archive,
                  modList: modList,
                  outputFolder: outputFolder, 
                  downloadFolder: downloadFolder,
                  parameters: parameters,
                  steps: 22,
                  game: modList.GameType)
        {
            var gameExe = Consts.GameFolderFilesDir.Combine(modList.GameType.MetaData().MainExecutable!);
            RedirectGamePath = modList.Directives.Any(d => d.To == gameExe);
        }

        public bool RedirectGamePath { get; }

        protected override async Task<bool> _Begin(CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return false;
            await Metrics.Send(Metrics.BeginInstall, ModList.Name);
            Utils.Log("Configuring Processor");

            FileExtractor2.FavorPerfOverRAM = FavorPerfOverRam;

            if (GameFolder == null)
                GameFolder = Game.TryGetGameLocation();

            if (GameFolder is { Exists: false })
            {
                Utils.Error($"Located game {Game.HumanFriendlyGameName} at \"{GameFolder.Value}\" but the folder does not exist!");
                return false;
            }
            
            if (GameFolder == null)
            {
                var otherGame = Game.CommonlyConfusedWith.Where(g => g.MetaData().IsInstalled).Select(g => g.MetaData()).FirstOrDefault();
                if (otherGame != null)
                {
                    Utils.Error(new CriticalFailureIntervention(
                        $"In order to do a proper install Wabbajack needs to know where your {Game.HumanFriendlyGameName} folder resides. However this game doesn't seem to be installed, we did however find an installed " +
                        $"copy of {otherGame.HumanFriendlyGameName}, did you install the wrong game?",
                        $"Could not locate {Game.HumanFriendlyGameName}"));
                }
                else
                {
                    Utils.Error(new CriticalFailureIntervention(
                        $"In order to do a proper install Wabbajack needs to know where your {Game.HumanFriendlyGameName} folder resides. However this game doesn't seem to be installed.",
                        $"Could not locate {Game.HumanFriendlyGameName}"));
                }

                Utils.Error("Exiting because we couldn't find the game folder.");
                return false;
            }

            Utils.Log($"Install Folder: {OutputFolder}");
            Utils.Log($"Downloads Folder: {DownloadFolder}");
            Utils.Log($"Game Folder: {GameFolder.Value}");
            Utils.Log($"Wabbajack Folder: {AbsolutePath.EntryPoint}");
            
            
            var watcher = new DiskSpaceWatcher(cancel, new[]{OutputFolder, DownloadFolder, GameFolder.Value, AbsolutePath.EntryPoint}, (long)2 << 31,
                drive =>
                {
                    Utils.Log($"Aborting due to low space on {drive.Name}");
                    Abort();
                });
            var watcherTask = watcher.Start();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Validating Game ESMs");
            await ValidateGameESMs();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Creating Output Folders");

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

            // Reduce to one thread if downloads on HDD, else use specified. Hashing on HDD has no benefit with more threads.
            if (new PhysicalDisk(DownloadFolder.DriveInfo().Name).MediaType == PhysicalDisk.MediaTypes.HDD && ReduceHDDThreads) DesiredThreads.OnNext(1); else DesiredThreads.OnNext(DiskThreads);

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Optimizing ModList");
            await OptimizeModlist();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Archives");
            await HashArchives();

            // Set to download thread count.
            DesiredThreads.OnNext(DownloadThreads);

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Downloading Missing Archives");
            await DownloadArchives();

            // Reduce to one thread if downloads on HDD, else use specified. Hashing on HDD has no benefit with more threads.
            if (new PhysicalDisk(DownloadFolder.DriveInfo().Name).MediaType == PhysicalDisk.MediaTypes.HDD && ReduceHDDThreads) DesiredThreads.OnNext(1); else DesiredThreads.OnNext(DiskThreads);

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Remaining Archives");
            await HashArchives();

            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            if (missing.Count > 0)
            {
                foreach (var a in missing)
                    Info($"Unable to download {a.Name} ({a.State.PrimaryKeyString})");
                if (IgnoreMissingFiles)
                    Info("Missing some archives, but continuing anyways at the request of the user");
                else
                    Error("Cannot continue, was unable to download one or more archives");
            }

            // Reduce to two threads if output on HDD, else use specified. Installing files seems to have a slight benefit with two threads.
            if (new PhysicalDisk(OutputFolder.DriveInfo().Name).MediaType == PhysicalDisk.MediaTypes.HDD && ReduceHDDThreads) DesiredThreads.OnNext(2); else DesiredThreads.OnNext(DiskThreads);

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Extracting Modlist contents");
            await ExtractModlist();

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

            UpdateTracker.NextStep("Updating System-specific ini settings and writing metadata");
            SetScreenSizeInPrefs();
            await InstalledModLists.AddModListInstall(Metadata, ModList, OutputFolder, DownloadFolder, ModListArchive);
            
            UpdateTracker.NextStep("Compacting files");
            await CompactFiles();

            UpdateTracker.NextStep("Installation complete! You may exit the program.");
            await ExtractedModlistFolder!.DisposeAsync();
            await Metrics.Send(Metrics.FinishInstall, ModList.Name);

            return true;
        }

        private async Task CompactFiles()
        {
            if (this.UseCompression)
            {
                await OutputFolder.CompactFolder(Queue, FileCompaction.Algorithm.XPRESS16K);
            }
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
            await ModList.Archives
                   .PMap(Queue, UpdateTracker, async archive =>
                   {
                       if (HashedArchives.TryGetValue(archive.Hash, out var paths))
                       {
                           var metaPath = paths.WithExtension(Consts.MetaFileExtension);
                           if (!metaPath.Exists && !(archive.State is GameFileSourceDownloader.State))
                           {
                               Status($"Writing {metaPath.FileName}");
                               var meta = AddInstalled(archive.State.GetMetaIni()).ToArray();
                               await metaPath.WriteAllLinesAsync(meta);
                           }
                       }
                   });
        }

        private IEnumerable<string> AddInstalled(string[] getMetaIni)
        {
            foreach (var f in getMetaIni)
            {
                yield return f;
                if (f == "[General]")
                {
                    yield return "installed=true";
                }
            }
        }

        private async ValueTask ValidateGameESMs()
        {
            foreach (var esm in ModList.Directives.OfType<CleanedESM>().ToList())
            {
                var filename = esm.To.FileName;
                var gameFile = GameFolder!.Value.Combine((RelativePath)"Data", filename);
                Utils.Log($"Validating {filename}");
                var hash = await gameFile.FileHashAsync();
                if (hash != esm.SourceESMHash)
                {
                    Utils.ErrorThrow(new InvalidGameESMError(esm, hash ?? Hash.Empty, gameFile));
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
                Info($"Building {bsa.To}");
                var sourceDir = OutputFolder.Combine(Consts.BSACreationDir, bsa.TempID);

                var bsaSize = bsa.FileStates.Select(state => sourceDir.Combine(state.Path).Size).Sum();

                await using var a = await bsa.State.MakeBuilder(bsaSize);
                var streams = await bsa.FileStates.PMap(Queue, UpdateTracker, async state =>
                {
                    Status($"Adding {state.Path} to BSA");
                    var fs = await sourceDir.Combine(state.Path).OpenRead();
                    await a.AddFile(state, fs);
                    return fs;
                });

                Info($"Writing {bsa.To}");
                await a.Build(OutputFolder.Combine(bsa.To));
                streams.Do(s => s.Dispose());

                await sourceDir.DeleteDirectory();
                // Write the expected hash so we ignore compression changes
                if (bsa.Hash != default) 
                    OutputFolder.Combine(bsa.To).FileHashWriteCache(bsa.Hash);

                if (UseCompression)
                    await OutputFolder.Combine(bsa.To).Compact(FileCompaction.Algorithm.XPRESS16K);
            }

            var bsaDir = OutputFolder.Combine(Consts.BSACreationDir);
            if (bsaDir.Exists)
            {
                Info($"Removing temp folder {Consts.BSACreationDir}");
                await Utils.DeleteDirectory(bsaDir);
            }
        }

        private async Task InstallIncludedFiles()
        {
            Info("Writing inline files");
            await ModList.Directives
                .OfType<InlineFile>()
                .PMap(Queue, UpdateTracker, async directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var outPath = OutputFolder.Combine(directive.To);
                    await outPath.DeleteAsync();

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

                    if (UseCompression)
                        await outPath.Compact(FileCompaction.Algorithm.XPRESS16K);
                });
        }

        private async Task GenerateCleanedESM(CleanedESM directive)
        {
            var filename = directive.To.FileName;
            var gameFile = GameFolder!.Value.Combine((RelativePath)"Data", filename);
            Info($"Generating cleaned ESM for {filename}");
            if (!gameFile.Exists) throw new InvalidDataException($"Missing {filename} at {gameFile}");
            Status($"Hashing game version of {filename}");
            var sha = await gameFile.FileHashCachedAsync();
            if (sha != directive.SourceESMHash)
                throw new InvalidDataException(
                    $"Cannot patch {filename} from the game folder because the hashes do not match. Have you already cleaned the file?");

            var patchData = await LoadBytesFromPath(directive.SourceDataID);
            var toFile = OutputFolder.Combine(directive.To);
            Status($"Patching {filename}");
            await using var output = await toFile.Create();
            await using var input = await gameFile.OpenRead();
            Utils.ApplyPatch(input, () => new MemoryStream(patchData), output);
        }

        private void SetScreenSizeInPrefs()
        {
            if (SystemParameters == null)
            {
                throw new ArgumentNullException("System Parameters was null.  Cannot set screen size prefs");
            }
            var config = new IniParserConfiguration {AllowDuplicateKeys = true, AllowDuplicateSections = true};
            var oblivionPath = (RelativePath)"Oblivion.ini";
            foreach (var file in OutputFolder.Combine("profiles").EnumerateFiles()
                .Where(f => ((string)f.FileName).EndsWith("refs.ini") || f.FileName == oblivionPath))
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
                catch (Exception)
                {
                    Utils.Log($"Skipping screen size remap for {file} due to parse error.");
                }
            }
            
            var tweaksPath = (RelativePath)"SSEDisplayTweaks.ini";
            foreach (var file in OutputFolder.EnumerateFiles()
                .Where(f => f.FileName == tweaksPath))
            {
                try
                {
                    var parser = new FileIniDataParser(new IniDataParser(config));
                    var data = parser.ReadFile((string)file);
                    bool modified = false;
                    if (data.Sections["Render"] != null)
                    {

                        if (data.Sections["Render"]["Resolution"] != null)
                        {
                            data.Sections["Render"]["Resolution"] =
                                $"{SystemParameters.ScreenWidth.ToString(CultureInfo.CurrentCulture)}x{SystemParameters.ScreenHeight.ToString(CultureInfo.CurrentCulture)}";
                            modified = true;
                        }

                    }
                    
                    if (modified) 
                        parser.WriteFile((string)file, data);
                }
                catch (Exception)
                {
                    Utils.Log($"Skipping screen size remap for {file} due to parse error.");
                }
            }
            
            // The Witcher 3
            if (this.Game.Game == Common.Game.Witcher3)
            {
                var name = (RelativePath)"user.settings";
                foreach (var file in OutputFolder.Combine("profiles").EnumerateFiles()
                             .Where(f => f.FileName == name))
                {
                    try
                    {
                        var parser = new FileIniDataParser(new IniDataParser(config));
                        var data = parser.ReadFile((string)file);
                        data["Viewport"]["Resolution"] =
                            $"\"{SystemParameters.ScreenWidth}x{SystemParameters.ScreenHeight}\"";
                        parser.WriteFile((string)file, data);
                    }
                    catch (Exception ex)
                    {
                        Utils.Log($"While remapping user.settings, {ex}");
                    }
                }
            }

        }

        private async Task WriteRemappedFile(RemappedInlineFile directive)
        {
            var data = Encoding.UTF8.GetString(await LoadBytesFromPath(directive.SourceDataID));

            var gameFolder = (string)(RedirectGamePath ? Consts.GameFolderFilesDir.RelativeTo(OutputFolder) : GameFolder!);


            data = data.Replace(Consts.GAME_PATH_MAGIC_BACK, gameFolder);
            data = data.Replace(Consts.GAME_PATH_MAGIC_DOUBLE_BACK, gameFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.GAME_PATH_MAGIC_FORWARD, gameFolder.Replace("\\", "/"));

            data = data.Replace(Consts.MO2_PATH_MAGIC_BACK, (string)OutputFolder);
            data = data.Replace(Consts.MO2_PATH_MAGIC_DOUBLE_BACK, ((string)OutputFolder).Replace("\\", "\\\\"));
            data = data.Replace(Consts.MO2_PATH_MAGIC_FORWARD, ((string)OutputFolder).Replace("\\", "/"));

            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_BACK, (string)DownloadFolder);
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK, ((string)DownloadFolder).Replace("\\", "\\\\"));
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_FORWARD, ((string)DownloadFolder).Replace("\\", "/"));

            await OutputFolder.Combine(directive.To).WriteAllTextAsync(data);
        }

        public static IErrorResponse CheckValidInstallPath(AbsolutePath path, AbsolutePath? downloadFolder, GameMetaData? game)
        {
            // Check if null path
            if (string.IsNullOrEmpty(path.ToString())) return ErrorResponse.Fail("Please select an install directory.");

            // Check if child of game folder
            if (game?.TryGetGameLocation() != null && path.IsChildOf(game.TryGetGameLocation())) return ErrorResponse.Fail("Cannot install to game directory.");

            // Check if child of Program Files
            var programFilesPath = KnownFolders.ProgramFiles.Path;
            if (programFilesPath != null)
            {
                if (path.IsChildOf(new AbsolutePath(programFilesPath))) return ErrorResponse.Fail("Cannot install to Program Files directory.");
            }

            // If the folder doesn't exist, it's empty so we don't need to check further
            if (!path.Exists) return ErrorResponse.Success;

            // Check folder does not have a Wabbajack ModList
            if (path.EnumerateFiles(false).Where(file => file.Exists).Any(file => file.Extension == Consts.ModListExtension))
            {
                return ErrorResponse.Fail($"Cannot install into a folder with a Wabbajack ModList inside of it.");
            }

            // Check if folder is empty
            if (path.IsEmptyDirectory)
            {
                return ErrorResponse.Success;
            }

            // Check if folders indicative of a previous install exist
            var checks = new List<RelativePath>() {
                Consts.MO2ModFolderName, 
                Consts.MO2ProfilesFolderName
            };
            if (checks.All(c => path.Combine(c).Exists))
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

            return ErrorResponse.Fail($"Either delete everything except the downloads folder, or pick a new location.  Cannot install to this folder as it has unexpected files.");
        }
    }
}
