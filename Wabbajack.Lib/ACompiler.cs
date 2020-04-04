using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.VirtualFileSystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public abstract class ACompiler : ABatchProcessor
    {
        public string ModListName, ModListAuthor, ModListDescription, ModListImage, ModListWebsite, ModListReadme;
        public bool ReadmeIsWebsite;
        public string WabbajackVersion;

        public abstract string VFSCacheName { get; }
        //protected string VFSCacheName => Path.Combine(Consts.LocalAppDataPath, $"vfs_compile_cache.bin");
        /// <summary>
        /// A stream of tuples of ("Update Title", 0.25) which represent the name of the current task
        /// and the current progress.
        /// </summary>
        public IObservable<(string, float)> ProgressUpdates => _progressUpdates;
        protected readonly Subject<(string, float)> _progressUpdates = new Subject<(string, float)>();

        public abstract ModManager ModManager { get; }

        public abstract string GamePath { get; }

        public abstract string ModListOutputFolder { get; }
        public abstract string ModListOutputFile { get; }

        public bool IgnoreMissingFiles { get; set; }

        public ICollection<Archive> SelectedArchives = new List<Archive>();
        public List<Directive> InstallDirectives = new List<Directive>();
        public List<RawSourceFile> AllFiles = new List<RawSourceFile>();
        public ModList ModList = new ModList();

        public List<IndexedArchive> IndexedArchives = new List<IndexedArchive>();
        public Dictionary<string, IEnumerable<VirtualFile>> IndexedFiles = new Dictionary<string, IEnumerable<VirtualFile>>();

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

        internal string IncludeFile(byte[] data)
        {
            var id = Guid.NewGuid().ToString();
            File.WriteAllBytes(Path.Combine(ModListOutputFolder, id), data);
            return id;
        }

        internal FileStream IncludeFile(out string id)
        {
            id = Guid.NewGuid().ToString();
            return File.Create(Path.Combine(ModListOutputFolder, id));
        }

        internal string IncludeFile(string data)
        {
            var id = Guid.NewGuid().ToString();
            File.WriteAllText(Path.Combine(ModListOutputFolder, id), data);
            return id;
        }

        public async Task<bool> GatherMetaData()
        {
            Utils.Log($"Getting meta data for {SelectedArchives.Count} archives");
            await SelectedArchives.PMap(Queue, async a =>
            {
                if (a.State is IMetaState metaState)
                {
                    if (string.IsNullOrWhiteSpace(metaState.URL))
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

        public void ExportModList()
        {
            Utils.Log($"Exporting ModList to {ModListOutputFile}");

            // Modify readme and ModList image to relative paths if they exist
            if (File.Exists(ModListImage))
            {
                ModList.Image = "modlist-image.png";
            }
            if (File.Exists(ModListReadme))
            {
                var readme = new FileInfo(ModListReadme);
                ModList.Readme = $"readme{readme.Extension}";
            }

            ModList.ReadmeIsWebsite = ReadmeIsWebsite;

            ModList.ToCERAS(Path.Combine(ModListOutputFolder, "modlist"), CerasConfig.Config);

            if (File.Exists(ModListOutputFile))
                File.Delete(ModListOutputFile);

            using (var fs = new FileStream(ModListOutputFile, FileMode.Create))
            {
                using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    Directory.EnumerateFiles(ModListOutputFolder, "*.*")
                        .DoProgress("Compressing ModList",
                    f =>
                    {
                        var ze = za.CreateEntry(Path.GetFileName(f));
                        using (var os = ze.Open())
                        using (var ins = File.OpenRead(f))
                        {
                            ins.CopyTo(os);
                        }
                    });

                    // Copy in modimage
                    if (File.Exists(ModListImage))
                    {
                        var ze = za.CreateEntry(ModList.Image);
                        using (var os = ze.Open())
                        using (var ins = File.OpenRead(ModListImage))
                        {
                            ins.CopyTo(os);
                        }
                    }

                    // Copy in readme
                    if (File.Exists(ModListReadme))
                    {
                        var ze = za.CreateEntry(ModList.Readme);
                        using (var os = ze.Open())
                        using (var ins = File.OpenRead(ModListReadme))
                        {
                            ins.CopyTo(os);
                        }
                    }
                }
            }

            Utils.Log("Exporting ModList metadata");
            var metadata = new DownloadMetadata
            {
                Size = File.GetSize(ModListOutputFile),
                Hash = ModListOutputFile.FileHash(),
                NumberOfArchives = ModList.Archives.Count,
                SizeOfArchives = ModList.Archives.Sum(a => a.Size),
                NumberOfInstalledFiles = ModList.Directives.Count,
                SizeOfInstalledFiles = ModList.Directives.Sum(a => a.Size)
            };
            metadata.ToJSON(ModListOutputFile + ".meta.json");


            Utils.Log("Removing ModList staging folder");
            Utils.DeleteDirectory(ModListOutputFolder);
        }

        public void GenerateManifest()
        {
            var manifest = new Manifest(ModList);
            manifest.ToJSON(ModListOutputFile + ".manifest.json");
        }

        public async Task GatherArchives()
        {
            Info("Building a list of archives based on the files required");

            var shas = InstallDirectives.OfType<FromArchive>()
                .Select(a => a.ArchiveHashPath[0])
                .Distinct();

            var archives = IndexedArchives.OrderByDescending(f => f.File.LastModified)
                .GroupBy(f => f.File.Hash)
                .ToDictionary(f => f.Key, f => f.First());

            SelectedArchives = await shas.PMap(Queue, sha => ResolveArchive(sha, archives));
        }

        public async Task<Archive> ResolveArchive(string sha, IDictionary<string, IndexedArchive> archives)
        {
            if (archives.TryGetValue(sha, out var found))
            {
                return await ResolveArchive(found);
            }

            Error($"No match found for Archive sha: {sha} this shouldn't happen");
            return null;
        }

        public async Task<Archive> ResolveArchive(IndexedArchive archive)
        {
            Utils.Status($"Checking link for {archive.Name}", alsoLog: true);

            if (archive.IniData == null)
                Error(
                    $"No download metadata found for {archive.Name}, please use MO2 to query info or add a .meta file and try again.");

            var result = new Archive
            {
                State = await DownloadDispatcher.ResolveArchive(archive.IniData)
            };

            if (result.State == null)
                Error($"{archive.Name} could not be handled by any of the downloaders");

            result.Name = archive.Name;
            result.Hash = archive.File.Hash;
            result.Meta = archive.Meta;
            result.Size = archive.File.Size;

            if (result.State != null && !await result.State.Verify(result))
                Error(
                    $"Unable to resolve link for {archive.Name}. If this is hosted on the Nexus the file may have been removed.");

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
