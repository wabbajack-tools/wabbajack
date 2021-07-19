using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1.Cms;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public abstract class ACompiler : ABatchProcessor
    {
        protected readonly Subject<(string, float)> _progressUpdates = new();

        public List<IndexedArchive> IndexedArchives = new();

        public Dictionary<Hash, IEnumerable<VirtualFile>> IndexedFiles = new();

        public ModList ModList = new();
        public AbsolutePath ModListImage;
        public bool ModlistIsNSFW;

        public string? ModListName, ModListAuthor, ModListDescription, ModListWebsite, ModlistReadme;
        public Version? ModlistVersion;
        protected Version? WabbajackVersion;

        public ACompiler(int steps, string modlistName, AbsolutePath sourcePath, AbsolutePath downloadsPath,
            AbsolutePath outputModListName)
            : base(steps)
        {
            SourcePath = sourcePath;
            DownloadsPath = downloadsPath;
            ModListName = modlistName;
            ModListOutputFile = outputModListName;
            //set in MainWindowVM
            WabbajackVersion = Consts.CurrentMinimumWabbajackVersion;
            Settings = new CompilerSettings();
            ModListOutputFolder = AbsolutePath.EntryPoint.Combine("output_folder", Guid.NewGuid().ToString());
            CompilingGame = new GameMetaData();
        }

        /// <summary>
        /// Set to true to include game files during compilation, only ever disabled
        /// in testing (to speed up tests)
        /// </summary>
        public bool UseGamePaths { get; set; } = true;

        public CompilerSettings Settings { get; set; }

        public AbsolutePath VFSCacheName =>
            Consts.LocalAppDataPath.Combine(
                $"vfs_compile_cache-2-{Path.Combine((string)SourcePath ?? "Unknown", "ModOrganizer.exe").StringSha256Hex()}.bin");

        //protected string VFSCacheName => Path.Combine(Consts.LocalAppDataPath, $"vfs_compile_cache.bin");
        /// <summary>
        /// A stream of tuples of ("Update Title", 0.25) which represent the name of the current task
        /// and the current progress.
        /// </summary>
        public IObservable<(string, float)> ProgressUpdates => _progressUpdates;

        public abstract AbsolutePath GamePath { get; }
        public Dictionary<Game, HashSet<Hash>> GameHashes { get; set; } = new Dictionary<Game, HashSet<Hash>>();
        public Dictionary<Hash, Game[]> GamesWithHashes { get; set; } = new Dictionary<Hash, Game[]>();

        public AbsolutePath SourcePath { get; }
        public AbsolutePath DownloadsPath { get; }

        public GameMetaData CompilingGame { get; set; }

        public AbsolutePath ModListOutputFolder { get; }
        public AbsolutePath ModListOutputFile { get; }

        public bool IgnoreMissingFiles { get; set; }

        public List<Archive> SelectedArchives { get; protected set; } = new List<Archive>();
        public List<Directive> InstallDirectives { get; protected set; } = new List<Directive>();
        public List<RawSourceFile> AllFiles { get; protected set; } = new List<RawSourceFile>();

        public Dictionary<AbsolutePath, IndexedArchive> ArchivesByFullPath { get; set; } =
            new Dictionary<AbsolutePath, IndexedArchive>();

        public static void Info(string msg)
        {
            Utils.Log(msg);
        }

        public void Status(string msg)
        {
            Queue.Report(msg, Percent.Zero);
        }

        public static void Error(string msg)
        {
            Utils.Log(msg);
            throw new Exception(msg);
        }

        internal RelativePath IncludeId()
        {
            return RelativePath.RandomFileName();
        }

        internal async Task<RelativePath> IncludeFile(byte[] data)
        {
            var id = IncludeId();
            await ModListOutputFolder.Combine(id).WriteAllBytesAsync(data);
            return id;
        }

        internal AbsolutePath IncludeFile(out RelativePath id)
        {
            id = IncludeId();
            return ModListOutputFolder.Combine(id);
        }

        internal async Task<RelativePath> IncludeFile(string data)
        {
            var id = IncludeId();
            await ModListOutputFolder.Combine(id).WriteAllTextAsync(data);
            return id;
        }

        internal async Task<RelativePath> IncludeFile(Stream data)
        {
            var id = IncludeId();
            await ModListOutputFolder.Combine(id).WriteAllAsync(data);
            return id;
        }

        internal async Task<RelativePath> IncludeFile(AbsolutePath data)
        {
            await using var stream = await data.OpenRead();
            return await IncludeFile(stream);
        }


        internal async Task<(RelativePath, AbsolutePath)> IncludeString(string str)
        {
            var id = IncludeId();
            var fullPath = ModListOutputFolder.Combine(id);
            await fullPath.WriteAllTextAsync(str);
            return (id, fullPath);
        }

        public async Task<bool> GatherMetaData()
        {
            Utils.Log($"Getting meta data for {SelectedArchives.Count} archives");
            await SelectedArchives.PMap(Queue, async a =>
            {
                if (a.State is IMetaState metaState)
                {
                    if (metaState.URL == null || metaState.URL == Consts.WabbajackOrg)
                        return;

                    var b = await metaState.LoadMetaData();
                    if (b) Utils.Log($"Getting meta data for {a.Name} was successful!");
                }
            });

            return true;
        }

        protected async Task IndexGameFileHashes()
        {
            if (UseGamePaths)
            {
                //taking the games in Settings.IncludedGames + currently compiling game so you can eg
                //include the stock game files if you are compiling for a VR game (ex: Skyrim + SkyrimVR)
                foreach (var ag in Settings.IncludedGames.Cons(CompilingGame.Game).Distinct())
                {
                    try
                    {
                        var files = await ClientAPI.GetExistingGameFiles(Queue, ag);
                        Utils.Log($"Including {files.Length} stock game files from {ag} as download sources");
                        GameHashes[ag] = files.Select(f => f.Hash).ToHashSet();

                        IndexedArchives.AddRange(files.Select(f =>
                        {
                            var meta = f.State.GetMetaIniString();
                            var ini = meta.LoadIniString();
                            var state = (GameFileSourceDownloader.State)f.State;
                            return new IndexedArchive(
                                VFS.Index.ByRootPath[ag.MetaData().GameLocation().Combine(state.GameFile)])
                            {
                                IniData = ini, Meta = meta, Name = state.GameFile.Munge().ToString()
                            };
                        }));
                    }
                    catch (Exception e)
                    {
                        Utils.Error(e, "Unable to find existing game files, skipping.");
                    }
                }

                GamesWithHashes = GameHashes.SelectMany(g => g.Value.Select(h => (g, h)))
                    .GroupBy(gh => gh.h)
                    .ToDictionary(gh => gh.Key, gh => gh.Select(p => p.g.Key).ToArray());
            }
        }

        protected async Task CleanInvalidArchivesAndFillState()
        {
            var remove = (await IndexedArchives.PMap(Queue, async a =>
            {
                try
                {
                    a.State = (await ResolveArchive(a)).State;
                    return null;
                }
                catch (Exception ex)
                {
                    Utils.Log(ex.ToString());
                    return a;
                }
            }))
                .NotNull().ToHashSet();

            if (remove.Count == 0)
            {
                return;
            }

            Utils.Log(
                $"Removing {remove.Count} archives from the compilation state, this is probably not an issue but reference this if you have compilation failures");
            remove.Do(r => Utils.Log($"Resolution failed for: ({r.File.Size} {r.File.Hash}) {r.File.FullPath}"));
            IndexedArchives.RemoveAll(a => remove.Contains(a));
        }

        protected async Task InferMetas()
        {
            async Task<bool> HasInvalidMeta(AbsolutePath filename)
            {
                var metaname = filename.WithExtension(Consts.MetaFileExtension);
                if (!metaname.Exists)
                {
                    return true;
                }

                try
                {
                    return await DownloadDispatcher.ResolveArchive(metaname.LoadIniFile()) == null;
                }
                catch (Exception e)
                {
                    Utils.ErrorThrow(e, $"Exception while checking meta {filename}");
                    return false;
                }
            }

            var to_find = (await DownloadsPath.EnumerateFiles()
                    .Where(f => f.Extension != Consts.MetaFileExtension && f.Extension != Consts.HashFileExtension)
                    .PMap(Queue, async f => await HasInvalidMeta(f) ? f : default))
                .Where(f => f.Exists)
                .ToList();

            if (to_find.Count == 0)
            {
                return;
            }

            Utils.Log($"Attempting to infer {to_find.Count} metas from the server.");

            await to_find.PMap(Queue, async f =>
            {
                var vf = VFS.Index.ByRootPath[f];

                var meta = await ClientAPI.InferDownloadState(vf.Hash);

                if (meta == null)
                {
                    await vf.AbsoluteName.WithExtension(Consts.MetaFileExtension).WriteAllLinesAsync(
                        "[General]",
                        "unknownArchive=true");
                    return;
                }

                Utils.Log($"Inferred .meta for {vf.FullPath.FileName}, writing to disk");
                await vf.AbsoluteName.WithExtension(Consts.MetaFileExtension)
                    .WriteAllTextAsync(meta.GetMetaIniString());
            });
        }

        protected async Task ExportModList()
        {
            Utils.Log($"Exporting ModList to {ModListOutputFile}");

            // Modify readme and ModList image to relative paths if they exist
            if (ModListImage.Exists)
            {
                ModList.Image = (RelativePath)"modlist-image.png";
            }

            await using (var of = await ModListOutputFolder.Combine("modlist").Create())
                ModList.ToJson(of);

            await ModListOutputFolder.Combine("sig")
                .WriteAllBytesAsync(((await ModListOutputFolder.Combine("modlist").FileHashAsync()) ?? Hash.Empty).ToArray());

            //await ClientAPI.SendModListDefinition(ModList);

            await ModListOutputFile.DeleteAsync();

            await using (var fs = await ModListOutputFile.Create())
            {
                using var za = new ZipArchive(fs, ZipArchiveMode.Create);

                await ModListOutputFolder.EnumerateFiles()
                    .DoProgress("Compressing ModList",
                        async f =>
                        {
                            var ze = za.CreateEntry((string)f.FileName);
                            await using var os = ze.Open();
                            await using var ins = await f.OpenRead();
                            await ins.CopyToAsync(os);
                        });

                // Copy in modimage
                if (ModListImage.Exists)
                {
                    var ze = za.CreateEntry((string)ModList.Image);
                    await using var os = ze.Open();
                    await using var ins = await ModListImage.OpenRead();
                    await ins.CopyToAsync(os);
                }
            }

            Utils.Log("Exporting Modlist metadata");
            var outputFileHash = await ModListOutputFile.FileHashAsync();
            if (outputFileHash == null)
            {
                Utils.Error("Unable to hash Modlist Output File");
                return;
            }
            
            var metadata = new DownloadMetadata
            {
                Size = ModListOutputFile.Size,
                Hash = outputFileHash.Value,
                NumberOfArchives = ModList.Archives.Count,
                SizeOfArchives = ModList.Archives.Sum(a => a.Size),
                NumberOfInstalledFiles = ModList.Directives.Count,
                SizeOfInstalledFiles = ModList.Directives.Sum(a => a.Size)
            };
            metadata.ToJson(ModListOutputFile + ".meta.json");

            Utils.Log("Removing ModList staging folder");
            await Utils.DeleteDirectory(ModListOutputFolder);
        }

        /// <summary>
        ///     Fills in the Patch fields in files that require them
        /// </summary>
        protected async Task BuildPatches()
        {
            Info("Gathering patch files");

            var toBuild = InstallDirectives.OfType<PatchedFromArchive>()
                .Where(p => p.Choices.Length > 0)
                .SelectMany(p => p.Choices.Select(c => new PatchedFromArchive
                {
                    To = p.To,
                    Hash = p.Hash,
                    ArchiveHashPath = c.MakeRelativePaths(),
                    FromFile = c,
                    Size = p.Size
                }))
                .ToArray();

            if (toBuild.Length == 0)
            {
                return;
            }

            // Extract all the source files
            var indexed = toBuild.GroupBy(f => VFS.Index.FileForArchiveHashPath(f.ArchiveHashPath))
                .ToDictionary(f => f.Key);
            await VFS.Extract(Queue, indexed.Keys.ToHashSet(),
                async (vf, sf) =>
                {
                    // For each, extract the destination
                    var matches = indexed[vf];
                    using var iqueue = new WorkQueue(1);
                    foreach (var match in matches)
                    {
                        var destFile = FindDestFile(match.To);
                        // Build the patch
                        await VFS.Extract(iqueue, new[] {destFile}.ToHashSet(),
                            async (destvf, destsfn) =>
                            {
                                Info($"Patching {match.To}");
                                Status($"Patching {match.To}");
                                await using var srcStream = await sf.GetStream();
                                await using var destStream = await destsfn.GetStream();
                                var patchSize =
                                    await Utils.CreatePatchCached(srcStream, vf.Hash, destStream, destvf.Hash);
                                Info($"Patch size {patchSize} for {match.To}");
                            });
                    }
                });

            // Load in the patches
            await InstallDirectives.OfType<PatchedFromArchive>()
                .Where(p => p.PatchID == default)
                .PMap(Queue, async pfa =>
                {
                    var patches = pfa.Choices
                        .Select(c => (Utils.TryGetPatch(c.Hash, pfa.Hash, out var data), data, c))
                        .ToArray();

                    // Pick the best patch
                    if (patches.All(p => p.Item1))
                    {
                        var (_, bytes, file) = IncludePatches.PickPatch(this, patches);
                        pfa.FromFile = file;
                        pfa.FromHash = file.Hash;
                        pfa.ArchiveHashPath = file.MakeRelativePaths();
                        pfa.PatchID = await IncludeFile(await bytes!.GetData());
                    }
                });

            var firstFailedPatch =
                InstallDirectives.OfType<PatchedFromArchive>().FirstOrDefault(f => f.PatchID == default);
            if (firstFailedPatch != null)
            {
                Utils.Log("Missing data from failed patch, starting data dump");
                Utils.Log($"Dest File: {firstFailedPatch.To}");
                Utils.Log($"Options ({firstFailedPatch.Choices.Length}:");
                foreach (var choice in firstFailedPatch.Choices)
                {
                    Utils.Log($"  {choice.FullPath}");
                }

                Error(
                    $"Missing patches after generation, this should not happen. First failure: {firstFailedPatch.FullPath}");
            }
        }

        private VirtualFile FindDestFile(RelativePath to)
        {
            var abs = to.RelativeTo(SourcePath);
            if (abs.Exists)
            {
                return VFS.Index.ByRootPath[abs];
            }

            if (to.StartsWith(Consts.BSACreationDir))
            {
                var bsaId = (RelativePath)((string)to).Split('\\')[1];
                var bsa = InstallDirectives.OfType<CreateBSA>().First(b => b.TempID == bsaId);
                var find = (RelativePath)Path.Combine(((string)to).Split('\\').Skip(2).ToArray());

                return VFS.Index.ByRootPath[SourcePath.Combine(bsa.To)].Children.First(c => c.RelativeName == find);
            }

            throw new ArgumentException($"Couldn't load data for {to}");
        }

        public void GenerateManifest()
        {
            var manifest = new Manifest(ModList);
            manifest.ToJson(ModListOutputFile + ".manifest.json");
        }

        public async Task GatherArchives()
        {
            Info("Building a list of archives based on the files required");

            var hashes = InstallDirectives.OfType<FromArchive>()
                .Select(a => a.ArchiveHashPath.BaseHash)
                .Distinct();

            var archives = IndexedArchives.OrderByDescending(f => f.File.LastModified)
                .GroupBy(f => f.File.Hash)
                .ToDictionary(f => f.Key, f => f.First());

            SelectedArchives.SetTo(await hashes.PMap(Queue, hash => ResolveArchive(hash, archives)));
        }

        public async Task<Archive> ResolveArchive(Hash hash, IDictionary<Hash, IndexedArchive> archives)
        {
            if (archives.TryGetValue(hash, out var found))
            {
                return await ResolveArchive(found);
            }

            throw new ArgumentException($"No match found for Archive sha: {hash.ToBase64()} this shouldn't happen");
        }

        public async Task<Archive> ResolveArchive([NotNull] IndexedArchive archive)
        {
            if (!string.IsNullOrWhiteSpace(archive.Name))
                Utils.Status($"Checking link for {archive.Name}", alsoLog: true);

            if (archive.IniData == null)
                Error(
                    $"No download metadata found for {archive.Name}, please use MO2 to query info or add a .meta file and try again.");

            var state = (AbstractDownloadState?)await DownloadDispatcher.ResolveArchive(archive.IniData);

            if (state == null)
                Error($"{archive.Name} could not be handled by any of the downloaders");

            var result = new Archive(state!)
            {
                Name = archive.Name ?? "", 
                Hash = archive.File.Hash, 
                Size = archive.File.Size
            };

            await result.State!.GetDownloader().Prepare();

            var token = new CancellationTokenSource();
            token.CancelAfter(Consts.MaxVerifyTime);
            if (!await result.State.Verify(result, token.Token))
                Error(
                    $"Unable to resolve link for {archive.Name}. If this is hosted on the Nexus the file may have been removed.");

            result.Meta = string.Join("\n", result.State!.GetMetaIni());


            return result;
        }

        public async Task<Directive> RunStack(IEnumerable<ICompilationStep> stack, RawSourceFile source)
        {
            Utils.Status($"Compiling {source.Path}");
            foreach (var step in stack)
            {
                var result = await step.Run(source);
                if (result != null) return result;
            }

            throw new InvalidDataException("Data fell out of the compilation stack");
        }

        public abstract IEnumerable<ICompilationStep> GetStack();
        public abstract IEnumerable<ICompilationStep> MakeStack();

        public static void PrintNoMatches(ICollection<NoMatch> noMatches)
        {
            const int max = 10;
            Info($"No match for {noMatches.Count} files");
            if (noMatches.Count > 0)
            {
                int count = 0;
                foreach (var file in noMatches)
                {
                    if (count++ < max)
                    {
                        Utils.Log($"     {file.To} - {file.Reason}");
                    }
                    else
                    {
                        Utils.LogStraightToFile($"     {file.To} - {file.Reason}");
                    }

                    if (count == max && noMatches.Count > max)
                    {
                        Utils.Log($"     ...");
                    }
                }
            }
        }

        protected async Task InlineFiles()
        {
            var grouped = ModList.Directives.OfType<InlineFile>()
                .Where(f => f.SourceDataID == default)
                .GroupBy(f => f.SourceDataFile)
                .Where(x => x.Key != null)
                .ToDictionary(f => f.Key!);

            if (grouped.Count == 0) return;
            await VFS.Extract(Queue, grouped.Keys.ToHashSet(), async (vf, sfn) =>
            {
                await using var stream = await sfn.GetStream();
                var id = await IncludeFile(stream);
                foreach (var file in grouped[vf])
                {
                    file.SourceDataID = id;
                    file.SourceDataFile = null;
                }
            });
        }


        public bool CheckForNoMatchExit(ICollection<NoMatch> noMatches)
        {
            if (noMatches.Count > 0)
            {
                if (IgnoreMissingFiles)
                {
                    Info("Continuing even though files were missing at the request of the user.");
                }
                else
                {
                    Info("Exiting due to no way to compile these files");
                    return true;
                }
            }

            return false;
        }
    }
}
