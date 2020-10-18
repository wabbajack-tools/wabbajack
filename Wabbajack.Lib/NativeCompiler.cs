using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Validation;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public class NativeCompiler : ACompiler
    {
        public NativeCompiler(NativeCompilerSettings settings, AbsolutePath sourcePath, AbsolutePath downloadsPath, AbsolutePath outputModListPath) 
            : base(3, settings.ModListName, sourcePath, downloadsPath, outputModListPath)
        {
            CompilingGame = settings.CompilingGame.MetaData();
            GamePath = CompilingGame.GameLocation();
            NativeSettings = settings;
        }

        public NativeCompilerSettings NativeSettings { get; set; }

        protected override async Task<bool> _Begin(CancellationToken cancel)
        {
            await Metrics.Send("begin_compiling", ModListName ?? "unknown");
            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            DesiredThreads.OnNext(DiskThreads);
            FileExtractor2.FavorPerfOverRAM = FavorPerfOverRam;

            UpdateTracker.Reset();
            UpdateTracker.NextStep("Gathering information");

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
                    Utils.Log($"Aborting due to low space on {drive.Name}");
                    Abort();
                });
            var watcherTask = watcher.Start();

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            List<AbsolutePath> roots = new List<AbsolutePath> {SourcePath, GamePath, DownloadsPath};
            roots.AddRange(Settings.IncludedGames.Select(g => g.MetaData().GameLocation()));

            UpdateTracker.NextStep("Indexing folders");

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            await VFS.AddRoots(roots);

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
                .PMap(Queue,
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
                        Utils.Log($"WELL THERE'S YOUR PROBLEM: {p} {VFS.Index.ByRootPath.Count}");
                    }

                    return new RawSourceFile(VFS.Index.ByRootPath[p], p.RelativeTo(SourcePath));
                });

            // If Game Folder Files exists, ignore the game folder
            IndexedFiles = IndexedArchives.SelectMany(f => f.File.ThisAndAllChildren)
                .OrderBy(f => f.NestingFactor)
                .GroupBy(f => f.Hash)
                .ToDictionary(f => f.Key, f => f.AsEnumerable());
            
            AllFiles.SetTo(mo2Files
                .DistinctBy(f => f.Path));

            Info($"Found {AllFiles.Count} files to build into mod list");

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Verifying destinations");

            var dups = AllFiles.GroupBy(f => f.Path)
                .Where(fs => fs.Count() > 1)
                .Select(fs =>
                {
                    Utils.Log(
                        $"Duplicate files installed to {fs.Key} from : {String.Join(", ", fs.Select(f => f.AbsolutePath))}");
                    return fs;
                }).ToList();

            if (dups.Count > 0)
            {
                Error($"Found {dups.Count} duplicates, exiting");
            }

            if (cancel.IsCancellationRequested)
            {
                return false;
            }

            UpdateTracker.NextStep("Loading INIs");

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
            var noMatch = results.OfType<NoMatch>().ToArray();
            PrintNoMatches(noMatch);
            if (CheckForNoMatchExit(noMatch))
            {
                return false;
            }

            foreach (var ignored in results.OfType<IgnoredDirectly>())
            {
                Utils.Log($"Ignored {ignored.To} because {ignored.Reason}");
            }

            InstallDirectives.SetTo(results.Where(i => !(i is IgnoredDirectly)));

            Info("Getting Nexus api_key, please click authorize if a browser window appears");

            UpdateTracker.NextStep("Building Patches");
            await BuildPatches();

            UpdateTracker.NextStep("Gathering Archives");
            await GatherArchives();

            UpdateTracker.NextStep("Gathering Metadata");
            await GatherMetaData();

            ModList = new ModList
            {
                GameType = CompilingGame.Game,
                WabbajackVersion = Consts.CurrentMinimumWabbajackVersion,
                Archives = SelectedArchives.ToList(),
                ModManager = ModManager.MO2,
                Directives = InstallDirectives,
                Name = ModListName ?? "untitled",
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
        
        /// <summary>
        ///     Clear references to lists that hold a lot of data.
        /// </summary>
        private void ResetMembers()
        {
            AllFiles = new List<RawSourceFile>();
            InstallDirectives = new List<Directive>();
            SelectedArchives = new List<Archive>();
        }

        public override AbsolutePath GamePath { get; }
        public override IEnumerable<ICompilationStep> GetStack()
        {
            return MakeStack();
        }

        public override IEnumerable<ICompilationStep> MakeStack()
        {
            List<ICompilationStep> steps = NativeSettings.CompilationSteps.Select(InterpretStep).ToList();
            steps.Add(new DropAll(this));
            return steps;
        }

        public ICompilationStep InterpretStep(string[] step)
        {
            return step[0] switch
            {
                "IgnoreStartsWith" => new IgnoreStartsWith(this, step[1]),
                "IncludeConfigs" => new IncludeAllConfigs(this),
                "IncludeDirectMatches" => new DirectMatch(this),
                "IncludePatches" => new IncludePatches(this),
                _ => throw new ArgumentException($"No interpretation for step {step[0]}")
            };
        }
    }
}
