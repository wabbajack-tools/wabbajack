using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Threading;
using CommonMark;
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

        protected static string _vfsCacheName = "vfs_compile_cache.bin";
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

        public bool ShowReportWhenFinished { get; set; } = true;

        public ICollection<Archive> SelectedArchives = new List<Archive>();
        public List<Directive> InstallDirectives = new List<Directive>();
        public List<RawSourceFile> AllFiles = new List<RawSourceFile>();
        public ModList ModList = new ModList();

        public List<IndexedArchive> IndexedArchives = new List<IndexedArchive>();
        public Dictionary<string, IEnumerable<VirtualFile>> IndexedFiles = new Dictionary<string, IEnumerable<VirtualFile>>();

        public void Info(string msg)
        {
            Utils.Log(msg);
        }

        public void Status(string msg)
        {
            Queue.Report(msg, 0);
        }

        public void Error(string msg)
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

        internal string IncludeFile(string data)
        {
            var id = Guid.NewGuid().ToString();
            File.WriteAllText(Path.Combine(ModListOutputFolder, id), data);
            return id;
        }

        public void ExportModList()
        {
            Utils.Log($"Exporting ModList to : {ModListOutputFile}");

            // Modify readme and modlist image to relative paths if they exist
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

            //ModList.ToJSON(Path.Combine(ModListOutputFolder, "modlist.json"));
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

        public void ShowReport()
        {
            if (!ShowReportWhenFinished) return;

            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, ModList.ReportHTML);
            Process.Start(file);
        }

        public void GenerateReport()
        {
            string css;
            using (var cssStream = Utils.GetEmbeddedResourceStream("Wabbajack.Lib.css-min.css"))
            {
                using (var reader = new StreamReader(cssStream))
                {
                    css = reader.ReadToEnd();
                }
            }

            using (var fs = File.OpenWrite($"{ModList.Name}.md"))
            {
                fs.SetLength(0);
                using (var reporter = new ReportBuilder(fs, ModListOutputFolder))
                {
                    reporter.Build(this, ModList);
                }
            }

            ModList.ReportHTML = "<style>" + css + "</style>"
                + CommonMarkConverter.Convert(File.ReadAllText($"{ModList.Name}.md"));
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

            if (result.State != null && !await result.State.Verify())
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
    }
}
