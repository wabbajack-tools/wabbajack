using Compression.BSA;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Lib.Validation;
using Wabbajack.VirtualFileSystem;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class MO2Compiler : ACompiler
    {
        private AbsolutePath _mo2DownloadsFolder;
        
        public AbsolutePath MO2Folder;

        public AbsolutePath MO2ModsFolder => MO2Folder.Combine(Consts.MO2ModFolderName);

        public string MO2Profile { get; }

        public override ModManager ModManager => ModManager.MO2;

        public override AbsolutePath GamePath { get; }

        public GameMetaData CompilingGame { get; }

        public override AbsolutePath ModListOutputFolder => ((RelativePath)"output_folder").RelativeToEntryPoint();

        public override AbsolutePath ModListOutputFile { get; }

        public override AbsolutePath VFSCacheName => 
            Consts.LocalAppDataPath.Combine( 
            $"vfs_compile_cache-2-{Path.Combine((string)MO2Folder ?? "Unknown", "ModOrganizer.exe").StringSha256Hex()}.bin");

        public dynamic MO2Ini { get; }

        public static AbsolutePath GetTypicalDownloadsFolder(AbsolutePath mo2Folder) => mo2Folder.Combine("downloads");

        public AbsolutePath MO2ProfileDir => MO2Folder.Combine("profiles", MO2Profile);

        public ConcurrentBag<Directive> ExtraFiles { get; private set; } = new ConcurrentBag<Directive>();
        public Dictionary<AbsolutePath, dynamic> ModInis { get; } = new Dictionary<AbsolutePath, dynamic>();

        public HashSet<string> SelectedProfiles { get; set; } = new HashSet<string>();

        public MO2Compiler(AbsolutePath mo2Folder, string mo2Profile, AbsolutePath outputFile)
            : base(steps: 20)
        {
            MO2Folder = mo2Folder;
            MO2Profile = mo2Profile;
            MO2Ini = MO2Folder.Combine("ModOrganizer.ini").LoadIniFile();
            var mo2game = (string)MO2Ini.General.gameName;
            CompilingGame = GameRegistry.Games.First(g => g.Value.MO2Name == mo2game).Value;
            GamePath = new AbsolutePath((string)MO2Ini.General.gamePath.Replace("\\\\", "\\"));
            ModListOutputFile = outputFile;
        }

        public AbsolutePath MO2DownloadsFolder
        {
            get
            {
                if (_mo2DownloadsFolder != default) return _mo2DownloadsFolder;
                if (MO2Ini != null)
                    if (MO2Ini.Settings != null)
                        if (MO2Ini.Settings.download_directory != null)
                            return MO2Ini.Settings.download_directory.Replace("/", "\\");
                return GetTypicalDownloadsFolder(MO2Folder);
            }
            set => _mo2DownloadsFolder = value;
        }

        protected override async Task<bool> _Begin(CancellationToken cancel)
        {
            var _ = Metrics.Send("begin_compiling", ModListName ?? "unknown");
            if (cancel.IsCancellationRequested) return false;
            Queue.SetActiveThreadsObservable(ConstructDynamicNumThreads(await RecommendQueueSize()));
            UpdateTracker.Reset();
            UpdateTracker.NextStep("Gathering information");
            Info("Looking for other profiles");
            var otherProfilesPath = MO2ProfileDir.Combine("otherprofiles.txt");
            SelectedProfiles = new HashSet<string>();
            if (otherProfilesPath.Exists) SelectedProfiles = (await otherProfilesPath.ReadAllLinesAsync()).ToHashSet();
            SelectedProfiles.Add(MO2Profile);

            Info("Using Profiles: " + string.Join(", ", SelectedProfiles.OrderBy(p => p)));

            Utils.Log($"VFS File Location: {VFSCacheName}");

            if (cancel.IsCancellationRequested) return false;
            await VFS.IntegrateFromFile(VFSCacheName);

            var roots = new List<AbsolutePath>
            {
                MO2Folder, GamePath, MO2DownloadsFolder, CompilingGame.GameLocation()
            };
            
            // TODO: make this generic so we can add more paths

            var lootPath = (AbsolutePath)Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LOOT");
            IEnumerable<RawSourceFile> lootFiles = new List<RawSourceFile>();
            if (lootPath.Exists)
            {
                roots.Add((AbsolutePath)lootPath);
            }
            UpdateTracker.NextStep("Indexing folders");

            if (cancel.IsCancellationRequested) return false;
            await VFS.AddRoots(roots);
            await VFS.WriteToFile(VFSCacheName);
            
            if (lootPath.Exists)
            {
                if (CompilingGame.MO2Name == null)
                {
                    throw new ArgumentException("Compiling game had no MO2 name specified.");
                }

                var lootGameDirs = new []
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
                        Utils.Log(
                            $"Found no LOOT user data for {CompilingGame.HumanFriendlyGameName} at {lootGameDir}!");
                }
            }
            
            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Cleaning output folder");
            await ModListOutputFolder.DeleteDirectory();
            
            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Inferring metas for game file downloads");
            await InferMetas();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Reindexing downloads after meta inferring");
            await VFS.AddRoot(MO2DownloadsFolder);
            await VFS.WriteToFile(VFSCacheName);
            

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Pre-validating Archives");
            

            // Find all Downloads
            IndexedArchives = (await MO2DownloadsFolder.EnumerateFiles()
                .Where(f => f.WithExtension(Consts.MetaFileExtension).Exists)
                .PMap(Queue, async f => new IndexedArchive(VFS.Index.ByRootPath[f])
                {
                    Name = (string)f.FileName,
                    IniData = f.WithExtension(Consts.MetaFileExtension).LoadIniFile(),
                    Meta = await f.WithExtension(Consts.MetaFileExtension).ReadAllTextAsync()
                })).ToList();


            var stockGameFolder = CompilingGame.GameLocation();
            
            foreach (var (relativePath, hash) in await ClientAPI.GetGameFiles(CompilingGame.Game, Version.Parse(CompilingGame.InstalledVersion)))
            {
                if (!VFS.Index.ByRootPath.TryGetValue(relativePath.RelativeTo(stockGameFolder), out var virtualFile))
                    continue;
                if (virtualFile.Hash != hash)
                {
                    Utils.Log(
                        $"File {relativePath} int the game folder appears to be modified, it will not be used during compilation");
                    continue;
                }

                var state = new GameFileSourceDownloader.State
                {
                    Game = CompilingGame.Game, GameVersion = CompilingGame.InstalledVersion, GameFile = relativePath
                };

                Utils.Log($"Adding Game file: {relativePath}");
                IndexedArchives.Add(new IndexedArchive(virtualFile)
                {
                    Name = (string)relativePath.FileName,
                    IniData = state.GetMetaIniString().LoadIniString(),
                    Meta = state.GetMetaIniString()
                });
            }
            
            
            
            await CleanInvalidArchives();

            UpdateTracker.NextStep("Finding Install Files");
            ModListOutputFolder.CreateDirectory();

            var mo2Files = MO2Folder.EnumerateFiles()
                .Where(p => p.IsFile)
                .Select(p =>
                {
                    if (!VFS.Index.ByRootPath.ContainsKey(p))
                        Utils.Log($"WELL THERE'S YOUR PROBLEM: {p} {VFS.Index.ByRootPath.Count}");
                    
                    return new RawSourceFile(VFS.Index.ByRootPath[p], p.RelativeTo(MO2Folder));
                });

            // If Game Folder Files exists, ignore the game folder
            IEnumerable<RawSourceFile> gameFiles;
            if (!MO2Folder.Combine(Consts.GameFolderFilesDir).Exists)
            {
                gameFiles = GamePath.EnumerateFiles()
                    .Where(p => p.IsFile)
                    .Where(p => p.Extension!= Consts.HashFileExtension)
                    .Select(p => new RawSourceFile(VFS.Index.ByRootPath[p],
                        Consts.GameFolderFilesDir.Combine(p.RelativeTo(GamePath))));
            }
            else
            {
                gameFiles = new List<RawSourceFile>();
            }


            IndexedFiles = IndexedArchives.SelectMany(f => f.File.ThisAndAllChildren)
                .OrderBy(f => f.NestingFactor)
                .GroupBy(f => f.Hash)
                .ToDictionary(f => f.Key, f => f.AsEnumerable());

            AllFiles.SetTo(mo2Files.Concat(gameFiles)
                .Concat(lootFiles)
                .DistinctBy(f => f.Path));

            Info($"Found {AllFiles.Count} files to build into mod list");

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Verifying destinations");

            var dups = AllFiles.GroupBy(f => f.Path)
                .Where(fs => fs.Count() > 1)
                .Select(fs =>
                {
                    Utils.Log($"Duplicate files installed to {fs.Key} from : {String.Join(", ", fs.Select(f => f.AbsolutePath))}");
                    return fs;
                }).ToList();

            if (dups.Count > 0)
            {
                Error($"Found {dups.Count} duplicates, exiting");
            }

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Loading INIs");

            ModInis.SetTo(MO2Folder.Combine(Consts.MO2ModFolderName)
                .EnumerateDirectories()
                .Select(f =>
                {
                    var modName = f.FileName;
                    var metaPath = f.Combine("meta.ini");
                    return metaPath.Exists ? (mod_name: f, metaPath.LoadIniFile()) : default;
                })
                .Where(f => f.Item1 != default)
                .Select(f => new KeyValuePair<AbsolutePath, dynamic>(f.Item1, f.Item2)));

            if (cancel.IsCancellationRequested) return false;
            var stack = MakeStack();
            UpdateTracker.NextStep("Running Compilation Stack");
            var results = await AllFiles.PMap(Queue, UpdateTracker, f => RunStack(stack, f));

            // Add the extra files that were generated by the stack
            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep($"Adding {ExtraFiles.Count} that were generated by the stack");
            results = results.Concat(ExtraFiles).ToArray();

            var noMatch = results.OfType<NoMatch>().ToArray();
            PrintNoMatches(noMatch);
            if (CheckForNoMatchExit(noMatch)) return false;

            InstallDirectives.SetTo(results.Where(i => !(i is IgnoredDirectly)));

            Info("Getting Nexus api_key, please click authorize if a browser window appears");

            UpdateTracker.NextStep("Verifying Files");
            zEditIntegration.VerifyMerges(this);

            UpdateTracker.NextStep("Gathering Archives");
            await GatherArchives();
            
            // Don't await this because we don't care if it fails.
            Utils.Log("Finding States to package");
            await AuthorAPI.UploadPackagedInis(SelectedArchives.ToArray());
            
            UpdateTracker.NextStep("Including Archive Metadata");
            await IncludeArchiveMetadata();
            UpdateTracker.NextStep("Building Patches");
            await BuildPatches();

            UpdateTracker.NextStep("Gathering Metadata");
            await GatherMetaData();

            ModList = new ModList
            {
                GameType = CompilingGame.Game,
                WabbajackVersion = WabbajackVersion,
                Archives = SelectedArchives.ToList(),
                ModManager = ModManager.MO2,
                Directives = InstallDirectives,
                Name = ModListName ?? MO2Profile,
                Author = ModListAuthor ?? "",
                Description = ModListDescription ?? "",
                Readme = ModlistReadme ?? "",
                Image = ModListImage != default ? ModListImage.FileName : default,
                Website = !string.IsNullOrWhiteSpace(ModListWebsite) ? new Uri(ModListWebsite) : null,
                Version = ModlistVersion ?? new Version(1,0,0,0),
                IsNSFW = ModlistIsNSFW
            };

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

        private async Task CleanInvalidArchives()
        {
            var remove = (await IndexedArchives.PMap(Queue, async a =>
            {
                try
                {
                    await ResolveArchive(a);
                    return null;
                }
                catch
                {
                    return a;
                }
            })).NotNull().ToHashSet();

            if (remove.Count == 0)
                return;

            Utils.Log(
                $"Removing {remove.Count} archives from the compilation state, this is probably not an issue but reference this if you have compilation failures");
            remove.Do(r => Utils.Log($"Resolution failed for: {r.File.FullPath}"));
            IndexedArchives.RemoveAll(a => remove.Contains(a));
        }

        private async Task InferMetas()
        {
            async Task<bool> HasInvalidMeta(AbsolutePath filename)
            {
                var metaname = filename.WithExtension(Consts.MetaFileExtension);
                if (!metaname.Exists) return true;
                return await DownloadDispatcher.ResolveArchive(metaname.LoadIniFile()) == null;
            }

            var to_find = (await MO2DownloadsFolder.EnumerateFiles()
                .Where(f => f.Extension != Consts.MetaFileExtension && f.Extension !=Consts.HashFileExtension)
                .PMap(Queue, async f => await HasInvalidMeta(f) ? f : default))
                .Where(f => f.Exists)
                .ToList();

            if (to_find.Count == 0) return;

            Utils.Log($"Attempting to infer {to_find.Count} metas from the server.");

            await to_find.PMap(Queue, async f =>
            {
                var vf = VFS.Index.ByRootPath[f];
                var client = new Common.Http.Client();
                using var response =
                    await client.GetAsync(
                        $"http://build.wabbajack.org/indexed_files/{vf.Hash.ToHex()}/meta.ini");

                if (!response.IsSuccessStatusCode)
                {
                    await vf.AbsoluteName.WithExtension(Consts.MetaFileExtension).WriteAllLinesAsync(
                        "[General]", 
                        "unknownArchive=true");
                    return;
                }

                var iniData = await response.Content.ReadAsStringAsync();
                Utils.Log($"Inferred .meta for {vf.FullPath.FileName}, writing to disk");
                await vf.AbsoluteName.WithExtension(Consts.MetaFileExtension).WriteAllTextAsync(iniData);
            });
        }

        private async Task IncludeArchiveMetadata()
        {
            Utils.Log($"Including {SelectedArchives.Count} .meta files for downloads");
            await SelectedArchives.PMap(Queue, async a =>
            {
                var source = MO2DownloadsFolder.Combine(a.Name + Consts.MetaFileExtension);
                var ini = a.State.GetMetaIniString();
                var (id, fullPath) = await IncludeString(ini);
                InstallDirectives.Add(new ArchiveMeta
                {
                    SourceDataID = id,
                    Size = source.Size,
                    Hash = await fullPath.FileHashAsync(),
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

        /// <summary>
        ///     Fills in the Patch fields in files that require them
        /// </summary>
        private async Task BuildPatches()
        {
            Info("Gathering patch files");

            await InstallDirectives.OfType<PatchedFromArchive>()
                .Where(p => p.PatchID == null)
                .PMap(Queue, async p =>
                {
                    if (Utils.TryGetPatch(p.FromHash, p.Hash, out var bytes))
                        p.PatchID = await IncludeFile(bytes);
                });

            var groups = InstallDirectives.OfType<PatchedFromArchive>()
                .Where(p => p.PatchID == default)
                .GroupBy(p => p.ArchiveHashPath.BaseHash)
                .ToList();

            Info($"Patching building patches from {groups.Count} archives");
            var absolutePaths = AllFiles.ToDictionary(e => e.Path, e => e.AbsolutePath);
            await groups.PMap(Queue, group => BuildArchivePatches(group.Key, group, absolutePaths));

            var firstFailedPatch = InstallDirectives.OfType<PatchedFromArchive>().FirstOrDefault(f => f.PatchID == null);
            if (firstFailedPatch != null)
                Error($"Missing patches after generation, this should not happen. First failure: {firstFailedPatch.FullPath}");
        }

        private async Task BuildArchivePatches(Hash archiveSha, IEnumerable<PatchedFromArchive> group,
            Dictionary<RelativePath, AbsolutePath> absolutePaths)
        {
            await using var files = await VFS.StageWith(@group.Select(g => VFS.Index.FileForArchiveHashPath(g.ArchiveHashPath)));
            var byPath = files.GroupBy(f => string.Join("|", f.FilesInFullPath.Skip(1).Select(i => i.Name)))
                .ToDictionary(f => f.Key, f => f.First());
            // Now Create the patches
            await @group.PMap(Queue, async entry =>
            {
                Info($"Patching {entry.To}");
                Status($"Patching {entry.To}");
                var srcFile = byPath[string.Join("|", entry.ArchiveHashPath.Paths)];
                await using var srcStream = srcFile.OpenRead();
                await using var outputStream = IncludeFile(out var id);
                entry.PatchID = id;
                await using var destStream = await LoadDataForTo(entry.To, absolutePaths);
                await Utils.CreatePatch(srcStream, srcFile.Hash, destStream, entry.Hash, outputStream);
                Info($"Patch size {outputStream.Length} for {entry.To}");
            });
        }

        private async Task<FileStream> LoadDataForTo(RelativePath to, Dictionary<RelativePath, AbsolutePath> absolutePaths)
        {
            if (absolutePaths.TryGetValue(to, out var absolute))
                return absolute.OpenRead();

            if (to.StartsWith(Consts.BSACreationDir))
            {
                var bsaId = (RelativePath)((string)to).Split('\\')[1];
                var bsa = InstallDirectives.OfType<CreateBSA>().First(b => b.TempID == bsaId);

                await using var a = BSADispatch.OpenRead(MO2Folder.Combine(bsa.To));
                var find = (RelativePath)Path.Combine(((string)to).Split('\\').Skip(2).ToArray());
                var file = a.Files.First(e => e.Path == find);
                var returnStream = new TempStream();
                file.CopyDataTo(returnStream);
                returnStream.Position = 0;
                return returnStream;
            }

            throw new ArgumentException($"Couldn't load data for {to}");
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
                new IgnoreStartsWith(this,"logs\\"),
                new IgnoreStartsWith(this, "downloads\\"),
                new IgnoreStartsWith(this,"webcache\\"),
                new IgnoreStartsWith(this, "overwrite\\"),
                new IgnoreStartsWith(this, "crashDumps\\"),
                new IgnorePathContains(this,"temporary_logs"),
                new IgnorePathContains(this, "GPUCache"),
                new IgnorePathContains(this, "SSEEdit Cache"),
                new IgnoreEndsWith(this, ".pyc"),
                new IgnoreEndsWith(this, ".log"),
                new IgnoreOtherProfiles(this),
                new IgnoreDisabledMods(this),
                new IncludeThisProfile(this),
                // Ignore the ModOrganizer.ini file it contains info created by MO2 on startup
                new IncludeStubbedConfigFiles(this),
                new IncludeLootFiles(this),
                new IgnoreRegex(this, Consts.GameFolderFilesDir + "\\\\.*\\.bsa"),
                new IncludeRegex(this, "^[^\\\\]*\\.bat$"),
                new IncludeModIniData(this),
                new DirectMatch(this),
                new IncludeTaggedMods(this, Consts.WABBAJACK_INCLUDE),
                new DeconstructBSAs(this), // Deconstruct BSAs before building patches so we don't generate massive patch files
                new IncludePatches(this),
                new IncludeDummyESPs(this),


                // If we have no match at this point for a game folder file, skip them, we can't do anything about them
                new IgnoreGameFiles(this),

                // There are some types of files that will error the compilation, because they're created on-the-fly via tools
                // so if we don't have a match by this point, just drop them.
                new IgnoreEndsWith(this, ".ini"),
                new IgnoreEndsWith(this, ".html"),
                new IgnoreEndsWith(this, ".txt"),
                // Don't know why, but this seems to get copied around a bit
                new IgnoreEndsWith(this, "HavokBehaviorPostProcess.exe"),
                // Theme file MO2 downloads somehow
                new IgnoreEndsWith(this, "splash.png"),
                // File to force MO2 into portable mode
                new IgnoreEndsWith(this, "portable.txt"), 
                new IgnoreEndsWith(this, ".bin"),
                new IgnoreEndsWith(this, ".refcache"),

                new IgnoreWabbajackInstallCruft(this),

                new PatchStockESMs(this),

                new IncludeAllConfigs(this),
                new zEditIntegration.IncludeZEditPatches(this),
                new IncludeTaggedMods(this, Consts.WABBAJACK_NOMATCH_INCLUDE),

                new DropAll(this)
            };
        }
    }
}
