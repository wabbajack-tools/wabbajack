using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public abstract class ACompiler : ABatchProcessor
    {
        public string? ModListName, ModListAuthor, ModListDescription, ModListWebsite, ModlistReadme;
        public Version? ModlistVersion;
        public AbsolutePath ModListImage;
        public bool ModlistIsNSFW;
        protected Version? WabbajackVersion;

        public abstract AbsolutePath VFSCacheName { get; }
        //protected string VFSCacheName => Path.Combine(Consts.LocalAppDataPath, $"vfs_compile_cache.bin");
        /// <summary>
        /// A stream of tuples of ("Update Title", 0.25) which represent the name of the current task
        /// and the current progress.
        /// </summary>
        public IObservable<(string, float)> ProgressUpdates => _progressUpdates;
        protected readonly Subject<(string, float)> _progressUpdates = new Subject<(string, float)>();

        public abstract ModManager ModManager { get; }

        public abstract AbsolutePath GamePath { get; }

        public abstract AbsolutePath ModListOutputFolder { get; }
        public abstract AbsolutePath ModListOutputFile { get; }

        public bool IgnoreMissingFiles { get; set; }

        public List<Archive> SelectedArchives { get; protected set; } = new List<Archive>();
        public List<Directive> InstallDirectives { get; protected set; } = new List<Directive>();
        public List<RawSourceFile> AllFiles { get; protected set; } = new List<RawSourceFile>();
        public ModList ModList = new ModList();

        public List<IndexedArchive> IndexedArchives = new List<IndexedArchive>();
        public Dictionary<Hash, IEnumerable<VirtualFile>> IndexedFiles = new Dictionary<Hash, IEnumerable<VirtualFile>>();

        public ACompiler(int steps)
            : base(steps)
        {
        }

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

        internal FileStream IncludeFile(out RelativePath id)
        {
            id = IncludeId();
            return ModListOutputFolder.Combine(id).Create();
        }

        internal async Task<RelativePath> IncludeFile(string data)
        {
            var id = IncludeId();
            await ModListOutputFolder.Combine(id).WriteAllTextAsync(data);
            return id;
        }
        
        internal async Task<RelativePath> IncludeFile(AbsolutePath data)
        {
            var id = IncludeId();
            await data.CopyToAsync(ModListOutputFolder.Combine(id));
            return id;
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
                    Utils.Log(b
                        ? $"Getting meta data for {a.Name} was successful!"
                        : $"Getting meta data for {a.Name} failed!");
                }
                else
                {
                    Utils.Log($"Archive {a.Name} is not an AbstractMetaState!");
                }
            });

            return true;
        }

        public async Task ExportModList()
        {
            Utils.Log($"Exporting ModList to {ModListOutputFile}");

            // Modify readme and ModList image to relative paths if they exist
            if (ModListImage.Exists)
            {
                ModList.Image = (RelativePath)"modlist-image.png";
            }

            using (var of = ModListOutputFolder.Combine("modlist").Create()) 
                ModList.ToJson(of);

            ModListOutputFile.Delete();

            using (var fs = ModListOutputFile.Create())
            {
                using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    ModListOutputFolder.EnumerateFiles()
                        .DoProgress("Compressing ModList",
                    f =>
                    {
                        var ze = za.CreateEntry((string)f.FileName);
                        using var os = ze.Open();
                        using var ins = f.OpenRead();
                        ins.CopyTo(os);
                    });

                    // Copy in modimage
                    if (ModListImage.Exists)
                    {
                        var ze = za.CreateEntry((string)ModList.Image);
                        using (var os = ze.Open())
                        using (var ins = ModListImage.OpenRead())
                        {
                            ins.CopyTo(os);
                        }
                    }
                }
            }

            Utils.Log("Exporting ModList metadata");
            var metadata = new DownloadMetadata
            {
                Size = ModListOutputFile.Size,
                Hash = ModListOutputFile.FileHash(),
                NumberOfArchives = ModList.Archives.Count,
                SizeOfArchives = ModList.Archives.Sum(a => a.Size),
                NumberOfInstalledFiles = ModList.Directives.Count,
                SizeOfInstalledFiles = ModList.Directives.Sum(a => a.Size)
            };
            metadata.ToJson(ModListOutputFile + ".meta.json");


            Utils.Log("Removing ModList staging folder");
            await Utils.DeleteDirectory(ModListOutputFolder);
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

        public async Task<Archive> ResolveArchive(IndexedArchive archive)
        {
            Utils.Status($"Checking link for {archive.Name}", alsoLog: true);

            if (archive.IniData == null)
                Error(
                    $"No download metadata found for {archive.Name}, please use MO2 to query info or add a .meta file and try again.");

            var result = new Archive(await DownloadDispatcher.ResolveArchive(archive.IniData));

            if (result.State == null)
                Error($"{archive.Name} could not be handled by any of the downloaders");

            result.Name = archive.Name;
            result.Hash = archive.File.Hash;
            result.Size = archive.File.Size;

            await result.State!.GetDownloader().Prepare();

            if (result.State != null && !await result.State.Verify(result))
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
