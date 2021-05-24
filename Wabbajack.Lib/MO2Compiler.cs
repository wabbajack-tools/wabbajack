using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.Validation;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public class MO2Compiler : ACompiler
    {
        public MO2Compiler(AbsolutePath sourcePath, AbsolutePath downloadsPath, string mo2Profile, AbsolutePath outputFile)
            : base(21, mo2Profile, sourcePath, downloadsPath, outputFile)
        {
            MO2Profile = mo2Profile;
            MO2Ini = SourcePath.Combine("ModOrganizer.ini").LoadIniFile();
            var mo2game = (string)MO2Ini.General.gameName;
            CompilingGame = GameRegistry.Games.First(g => g.Value.MO2Name == mo2game).Value;
            GamePath = CompilingGame.GameLocation();
        }

        public AbsolutePath MO2ModsFolder => SourcePath.Combine(Consts.MO2ModFolderName);

        public string MO2Profile { get; }

        public override AbsolutePath GamePath { get; }

        public dynamic MO2Ini { get; }

        public AbsolutePath MO2ProfileDir => SourcePath.Combine("profiles", MO2Profile);

        public ConcurrentBag<Directive> ExtraFiles { get; private set; } = new ConcurrentBag<Directive>();
        public Dictionary<AbsolutePath, dynamic> ModInis { get; } = new Dictionary<AbsolutePath, dynamic>();

        public HashSet<string> SelectedProfiles { get; set; } = new HashSet<string>();

        public static AbsolutePath GetTypicalDownloadsFolder(AbsolutePath mo2Folder)
        {
            return mo2Folder.Combine("downloads");
        }

        protected override async Task<bool> _Begin(CancellationToken cancel)
        {
            await Metrics.Send("begin_compiling", MO2Profile ?? "unknown");
            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            DesiredThreads.OnNext(DiskThreads);
            FileExtractor2.FavorPerfOverRAM = FavorPerfOverRam;

            UpdateTracker.Reset();
            UpdateTracker.NextStep("Gathering information");

            Utils.Log("Loading compiler Settings");
            Settings = await CompilerSettings.Load(MO2ProfileDir);
            Settings.IncludedGames = Settings.IncludedGames.Add(CompilingGame.Game);

            Utils.Log("Looking for other profiles");
            var otherProfilesPath = MO2ProfileDir.Combine("otherprofiles.txt");
            SelectedProfiles = new HashSet<string>();
            if (otherProfilesPath.Exists)
            {
                SelectedProfiles = (await otherProfilesPath.ReadAllLinesAsync()).ToHashSet();
            }

            SelectedProfiles.Add(MO2Profile!);

            Utils.Log("Using Profiles: " + string.Join(", ", SelectedProfiles.OrderBy(p => p)));

            Utils.Log($"Compiling Game: {CompilingGame.Game}");
            Utils.Log("Games from setting files:");
            foreach (var game in Settings.IncludedGames)
            {
                Utils.Log($"- {game}");
            }

            Utils.Log($"VFS File Location: {VFSCacheName}");
            Utils.Log($"MO2 Folder: {SourcePath}");
            Utils.Log($"Downloads Folder: {DownloadsPath}");
            Utils.Log($"Game Folder: {GamePath}");

            var watcher = new DiskSpaceWatcher(cancel,
                new[] {SourcePath, DownloadsPath, GamePath, AbsolutePath.EntryPoint}, (long)2 << 31,
                drive =>
                {
                    Utils.Error($"Aborting due to low space on {drive.Name}");
                    Abort();
                });
            var watcherTask = watcher.Start();

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            List<AbsolutePath> roots;
            if (UseGamePaths)
            {
                roots = new List<AbsolutePath> {SourcePath, GamePath, DownloadsPath};
                roots.AddRange(Settings.IncludedGames.Select(g => g.MetaData().GameLocation()));
            }
            else
            {
                roots = new List<AbsolutePath> {SourcePath, DownloadsPath};
            }

            // TODO: make this generic so we can add more paths

            var lootPath = (AbsolutePath)Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LOOT");
            IEnumerable<RawSourceFile> lootFiles = new List<RawSourceFile>();
            if (lootPath.Exists)
            {
                roots.Add(lootPath);
            }

            UpdateTracker.NextStep("Indexing folders");

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            await VFS.AddRoots(roots);

            if (lootPath.Exists)
            {
                if (CompilingGame.MO2Name == null)
                {
                    throw new ArgumentException("Compiling game had no MO2 name specified.");
                }

                var lootGameDirs = new[]
                {
                    CompilingGame.MO2Name, // most of the games use the MO2 name
                    CompilingGame.MO2Name.Replace(" ", "") //eg: Fallout 4 -> Fallout4
                };

                var lootGameDir = lootGameDirs.Select(x => lootPath.Combine(x))
                    .FirstOrDefault(p => p.IsDirectory);

                if (lootGameDir != default)
                {
                    Utils.Log($"Found LOOT game folder at {lootGameDir}");
                    lootFiles = lootGameDir.EnumerateFiles(false)
                        .Where(p => p.FileName == (RelativePath)"userlist.yaml")
                        .Where(p => p.IsFile)
                        .Select(p => new RawSourceFile(VFS.Index.ByRootPath[p],
                            Consts.LOOTFolderFilesDir.Combine(p.RelativeTo(lootPath))));

                    if (!lootFiles.Any())
                    {
                        Utils.Warn($"Found no LOOT user data for {CompilingGame.HumanFriendlyGameName} at {lootGameDir}!");
                    }
                }
            }

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Cleaning output folder");
            await ModListOutputFolder.DeleteDirectory();

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Inferring metas for game file downloads");
            await InferMetas();

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Reindexing downloads after meta inferring");
            await VFS.AddRoot(DownloadsPath);

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Pre-validating Archives");


            // Find all Downloads
            IndexedArchives = (await DownloadsPath.EnumerateFiles()
                .Where(f => f.WithExtension(Consts.MetaFileExtension).Exists)
                .PMap(Queue, UpdateTracker,
                    async f => new IndexedArchive(VFS.Index.ByRootPath[f])
                    {
                        Name = (string)f.FileName,
                        IniData = f.WithExtension(Consts.MetaFileExtension).LoadIniFile(),
                        Meta = await f.WithExtension(Consts.MetaFileExtension).ReadAllTextAsync()
                    })).ToList();


            await IndexGameFileHashes();

            IndexedArchives = IndexedArchives.DistinctBy(a => a.File.AbsoluteName).ToList();

            await CleanInvalidArchivesAndFillState();

            UpdateTracker.NextStep("Finding Install Files");
            ModListOutputFolder.CreateDirectory();

            var mo2Files = SourcePath.EnumerateFiles()
                .Where(p => p.IsFile)
                .Select(p =>
                {
                    if (!VFS.Index.ByRootPath.ContainsKey(p))
                    {
                        Utils.Error($"WELL THERE'S YOUR PROBLEM: {p} {VFS.Index.ByRootPath.Count}");
                    }

                    return new RawSourceFile(VFS.Index.ByRootPath[p], p.RelativeTo(SourcePath));
                });

            // If Game Folder Files exists, ignore the game folder
            IndexedFiles = IndexedArchives.SelectMany(f => f.File.ThisAndAllChildren)
                .OrderBy(f => f.NestingFactor)
                .GroupBy(f => f.Hash)
                .ToDictionary(f => f.Key, f => f.AsEnumerable());

            AllFiles.SetTo(mo2Files
                .Concat(lootFiles)
                .DistinctBy(f => f.Path));

            Utils.Log($"Found {AllFiles.Count} files to build into mod list");

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Verifying destinations");

            var dups = AllFiles.GroupBy(f => f.Path)
                .Where(fs => fs.Count() > 1)
                .Select(fs =>
                {
                    Utils.Error($"Duplicate files installed to {fs.Key} from : {String.Join(", ", fs.Select(f => f.AbsolutePath))}");
                    return fs;
                }).ToList();

            if (dups.Count > 0)
            {
                Utils.Fatal(new Exception($"Found {dups.Count} duplicates, exiting"));
            }

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Loading INIs");

            ModInis.SetTo(SourcePath.Combine(Consts.MO2ModFolderName)
                .EnumerateDirectories()
                .Select(f =>
                {
                    var modName = f.FileName;
                    var metaPath = f.Combine("meta.ini");
                    return metaPath.Exists ? (mod_name: f, metaPath.LoadIniFile()) : default;
                })
                .Where(f => f.Item1 != default)
                .Select(f => new KeyValuePair<AbsolutePath, dynamic>(f.Item1, f.Item2)));

            ArchivesByFullPath = IndexedArchives.ToDictionary(a => a.File.AbsoluteName);

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            var stack = MakeStack();
            UpdateTracker.NextStep("Running Compilation Stack");
            var results = await AllFiles.PMap(Queue, UpdateTracker, f => RunStack(stack, f));

            // Add the extra files that were generated by the stack
            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep($"Adding {ExtraFiles.Count} that were generated by the stack");
            results = results.Concat(ExtraFiles).ToArray();

            var noMatch = results.OfType<NoMatch>().ToArray();
            PrintNoMatches(noMatch);
            if (CheckForNoMatchExit(noMatch))
            {
                return false;
            }

            foreach (var ignored in results.OfType<IgnoredDirectly>())
            {
                Utils.Trace($"Ignored {ignored.To} because {ignored.Reason}");
            }

            InstallDirectives.SetTo(results.Where(i => !(i is IgnoredDirectly)));

            Utils.Log("Getting Nexus api_key, please click authorize if a browser window appears");

            UpdateTracker.NextStep("Verifying Files");
            zEditIntegration.VerifyMerges(this);

            UpdateTracker.NextStep("Building Patches");
            await BuildPatches();

            UpdateTracker.NextStep("Gathering Archives");
            await GatherArchives();

            UpdateTracker.NextStep("Including Archive Metadata");
            await IncludeArchiveMetadata();

            UpdateTracker.NextStep("Gathering Metadata");
            await GatherMetaData();

            ModList = new ModList
            {
                GameType = CompilingGame.Game,
                WabbajackVersion = Consts.CurrentMinimumWabbajackVersion,
                Archives = SelectedArchives.ToList(),
                ModManager = ModManager.MO2,
                Directives = InstallDirectives,
                Name = ModListName ?? MO2Profile!,
                Author = ModListAuthor ?? "",
                Description = ModListDescription ?? "",
                Readme = ModlistReadme ?? "",
                Image = ModListImage != default ? ModListImage.FileName : default,
                Website = !string.IsNullOrWhiteSpace(ModListWebsite) ? new Uri(ModListWebsite) : null,
                Version = ModlistVersion ?? new Version(1, 0, 0, 0),
                IsNSFW = ModlistIsNSFW
            };

            UpdateTracker.NextStep("Including required files");
            await InlineFiles();

            UpdateTracker.NextStep("Running Validation");

            await ValidateModlist.RunValidation(ModList);
            UpdateTracker.NextStep("Generating Report");

            GenerateManifest();

            UpdateTracker.NextStep("Exporting Modlist");
            await ExportModList();

            ResetMembers();

            UpdateTracker.NextStep("Done Building Modlist");

            return true;
        }

        private async Task IncludeArchiveMetadata()
        {
            Utils.Log($"Including {SelectedArchives.Count} .meta files for downloads");
            await SelectedArchives.PMap(Queue, async a =>
            {
                if (a.State is GameFileSourceDownloader.State)
                {
                    return;
                }

                var source = DownloadsPath.Combine(a.Name + Consts.MetaFileExtension);
                var ini = a.State.GetMetaIniString();
                var (id, fullPath) = await IncludeString(ini);
                var hash = await fullPath.FileHashAsync();

                if (hash == null) return;
                
                InstallDirectives.Add(new ArchiveMeta
                {
                    SourceDataID = id,
                    Size = fullPath.Size,
                    Hash = hash.Value,
                    To = source.FileName
                });
            });
        }

        /// <summary>
        ///     Clear references to lists that hold a lot of data.
        /// </summary>
        private void ResetMembers()
        {
            AllFiles = new List<RawSourceFile>();
            InstallDirectives = new List<Directive>();
            SelectedArchives = new List<Archive>();
            ExtraFiles = new ConcurrentBag<Directive>();
        }

        public override IEnumerable<ICompilationStep> GetStack()
        {
            return MakeStack();
        }

        /// <summary>
        ///     Creates a execution stack. The stack should be passed into Run stack. Each function
        ///     in this stack will be run in-order and the first to return a non-null result will have its
        ///     result included into the pack
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<ICompilationStep> MakeStack()
        {
            Utils.Log("Generating compilation stack");
            return new List<ICompilationStep>
            {
                new IgnoreGameFilesIfGameFolderFilesExist(this),
                new IncludePropertyFiles(this),
                //new IncludeSteamWorkshopItems(this),
                new IgnoreSaveFiles(this),
                new IgnoreStartsWith(this, "logs\\"),
                new IgnoreStartsWith(this, "downloads\\"),
                new IgnoreStartsWith(this, "webcache\\"),
                new IgnoreStartsWith(this, "overwrite\\"),
                new IgnoreStartsWith(this, "crashDumps\\"),
                new IgnorePathContains(this, "temporary_logs"),
                new IgnorePathContains(this, "GPUCache"),
                new IgnorePathContains(this, "SSEEdit Cache"),
                new IgnoreOtherProfiles(this),
                new IgnoreDisabledMods(this),
                new IgnoreTaggedFiles(this, Consts.WABBAJACK_IGNORE_FILES),
                new IgnoreTaggedFolders(this,Consts.WABBAJACK_IGNORE),
                new IncludeThisProfile(this),
                // Ignore the ModOrganizer.ini file it contains info created by MO2 on startup
                new IncludeStubbedConfigFiles(this),
                new IncludeLootFiles(this),
                new IgnoreStartsWith(this, Path.Combine((string)Consts.GameFolderFilesDir, "Data")),
                new IgnoreStartsWith(this, Path.Combine((string)Consts.GameFolderFilesDir, "Papyrus Compiler")),
                new IgnoreStartsWith(this, Path.Combine((string)Consts.GameFolderFilesDir, "Skyrim")),
                new IgnoreRegex(this, Consts.GameFolderFilesDir + "\\\\.*\\.bsa"),
                new IncludeRegex(this, "^[^\\\\]*\\.bat$"),
                new IncludeModIniData(this),
                new DirectMatch(this),
                new IncludeTaggedMods(this, Consts.WABBAJACK_INCLUDE),
                new IncludeTaggedFolders(this, Consts.WABBAJACK_INCLUDE),
                new IgnoreEndsWith(this, ".pyc"),
                new IgnoreEndsWith(this, ".log"),
                new DeconstructBSAs(
                    this), // Deconstruct BSAs before building patches so we don't generate massive patch files
                new IncludePatches(this),
                new IncludeDummyESPs(this),

                // There are some types of files that will error the compilation, because they're created on-the-fly via tools
                // so if we don't have a match by this point, just drop them.
                new IgnoreEndsWith(this, ".html"),                
                // Don't know why, but this seems to get copied around a bit
                new IgnoreEndsWith(this, "HavokBehaviorPostProcess.exe"),
                // Theme file MO2 downloads somehow
                new IncludeRegex(this, "splash\\.png"),
                // File to force MO2 into portable mode
                new IgnoreEndsWith(this, "portable.txt"),
                new IgnoreEndsWith(this, ".bin"),
                new IgnoreEndsWith(this, ".refcache"),
                //Include custom categories  
                new IncludeRegex(this, "categories.dat$"),
                new IgnoreWabbajackInstallCruft(this),

                //new PatchStockESMs(this),

                new IncludeAllConfigs(this),
                new zEditIntegration.IncludeZEditPatches(this),
                new IncludeTaggedMods(this, Consts.WABBAJACK_NOMATCH_INCLUDE),
                new IncludeTaggedFolders(this,Consts.WABBAJACK_NOMATCH_INCLUDE),
                new IncludeTaggedFiles(this,Consts.WABBAJACK_NOMATCH_INCLUDE_FILES),
                new IncludeRegex(this, ".*\\.txt"),
                new IgnorePathContains(this,@"\Edit Scripts\Export\"),
                new IgnoreExtension(this, new Extension(".CACHE")),
                new DropAll(this)
            };
        }
    }
}
