using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;

namespace Wabbajack.Lib
{
    public class VortexCompiler : ACompiler
    {
        public string GameName { get; }

        public string VortexFolder { get; }
        public string StagingFolder { get; }
        public string DownloadsFolder { get; }

        public bool IgnoreMissingFiles { get; set; }

        public VortexCompiler(string gameName, string gamePath)
        {
            _vortexCompiler = this;
            _mo2Compiler = null;
            ModManager = ModManager.Vortex;

            // TODO: only for testing
            IgnoreMissingFiles = true;

            GamePath = gamePath;
            GameName = gameName;

            // currently only works if staging and downloads folder is in the standard directory
            // aka %APPDATADA%\Vortex\
            VortexFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vortex");
            StagingFolder = Path.Combine(VortexFolder, gameName, "mods");
            DownloadsFolder = Path.Combine(VortexFolder, "downloads", gameName);

            ModListOutputFolder = "output_folder";

            // TODO: add custom modlist name
            ModListOutputFile = $"VORTEX_TEST_MODLIST{ExtensionManager.Extension}";

            VFS = VirtualFileSystem.VFS;

            SelectedArchives = new List<Archive>();
            AllFiles = new List<RawSourceFile>();
            IndexedArchives = new List<IndexedArchive>();
            IndexedFiles = new Dictionary<string, IEnumerable<VirtualFile>>();
        }

        public override void Info(string msg)
        {
            Utils.Log(msg);
        }

        public override void Status(string msg)
        {
            WorkQueue.Report(msg, 0);
        }

        public override void Error(string msg)
        {
            Utils.Log(msg);
            throw new Exception(msg);
        }

        internal override string IncludeFile(byte[] data)
        {
            var id = Guid.NewGuid().ToString();
            File.WriteAllBytes(Path.Combine(ModListOutputFolder, id), data);
            return id;
        }

        internal override string IncludeFile(string data)
        {
            var id = Guid.NewGuid().ToString();
            File.WriteAllText(Path.Combine(ModListOutputFolder, id), data);
            return id;
        }

        public override bool Compile()
        {
            VirtualFileSystem.Clean();
            Info($"Starting Vortex compilation for {GameName} at {GamePath} with staging folder at {StagingFolder} and downloads folder at  {DownloadsFolder}.");

            Info($"Indexing {GamePath}");
            VFS.AddRoot(GamePath);

            Info($"Indexing {DownloadsFolder}");
            VFS.AddRoot(DownloadsFolder);

            Info("Cleaning output folder");
            if (Directory.Exists(ModListOutputFolder)) Directory.Delete(ModListOutputFolder, true);
            Directory.CreateDirectory(ModListOutputFolder);

            IEnumerable<RawSourceFile> game_files = Directory.EnumerateFiles(GamePath, "*", SearchOption.AllDirectories)
                .Where(p => p.FileExists())
                .Select(p => new RawSourceFile(VFS.Lookup(p))
                    { Path = Path.Combine(Consts.GameFolderFilesDir, p.RelativeTo(GamePath)) });

            Info("Indexing Archives");
            IndexedArchives = Directory.EnumerateFiles(DownloadsFolder)
                //.Where(f => File.Exists(f+".meta"))
                .Where(File.Exists)
                .Select(f => new IndexedArchive
                {
                    File = VFS.Lookup(f),
                    Name = Path.GetFileName(f),
                    //IniData = (f+"meta").LoadIniFile(),
                    //Meta = File.ReadAllText(f+".meta")
                })
                .ToList();

            Info("Indexing Files");
            IDictionary<VirtualFile, IEnumerable<VirtualFile>> grouped = VFS.GroupedByArchive();
            IndexedFiles = IndexedArchives.Select(f => grouped.TryGetValue(f.File, out var result) ? result : new List<VirtualFile>())
                .SelectMany(fs => fs)
                .Concat(IndexedArchives.Select(f => f.File))
                .OrderByDescending(f => f.TopLevelArchive.LastModified)
                .GroupBy(f => f.Hash)
                .ToDictionary(f => f.Key, f => f.AsEnumerable());

            Info("Searching for mod files");
            AllFiles = game_files.DistinctBy(f => f.Path).ToList();

            Info($"Found {AllFiles.Count} files to build into mod list");

            Info("Verifying destinations");
            List<IGrouping<string, RawSourceFile>> dups = AllFiles.GroupBy(f => f.Path)
                .Where(fs => fs.Count() > 1)
                .Select(fs =>
                {
                    Utils.Log($"Duplicate files installed to {fs.Key} from : {string.Join(", ", fs.Select(f => f.AbsolutePath))}");
                    return fs;
                }).ToList();

            if (dups.Count > 0)
            {
                Error($"Found {dups.Count} duplicates, exiting");
            }

            IEnumerable<ICompilationStep> stack = MakeStack();

            Info("Running Compilation Stack");
            List<Directive> results = AllFiles.PMap(f => RunStack(stack, f)).ToList();

            IEnumerable<NoMatch> noMatch = results.OfType<NoMatch>().ToList();
            Info($"No match for {noMatch.Count()} files");
            foreach (var file in noMatch)
                Info($"     {file.To}");
            if (noMatch.Any())
            {
                if (IgnoreMissingFiles)
                {
                    Info("Continuing even though files were missing at the request of the user.");
                }
                else
                {
                    Info("Exiting due to no way to compile these files");
                    return false;
                }
            }

            InstallDirectives = results.Where(i => !(i is IgnoredDirectly)).ToList();

            // TODO: nexus stuff
            /*Info("Getting Nexus api_key, please click authorize if a browser window appears");
            if (IndexedArchives.Any(a => a.IniData?.General?.gameName != null))
            {
                var nexusClient = new NexusApiClient();
                if (!nexusClient.IsPremium) Error($"User {nexusClient.Username} is not a premium Nexus user, so we cannot access the necessary API calls, cannot continue");

            }
            */

            GatherArchives();

            ModList = new ModList
            {
                Archives = SelectedArchives,
                ModManager = ModManager.Vortex,
                Directives = InstallDirectives
            };
            
            ExportModList();

            Info("Done Building ModList");
            return true;
        }

        private void ExportModList()
        {
            Utils.Log($"Exporting ModList to: {ModListOutputFolder}");

            // using JSON for better debugging
            ModList.ToJSON(Path.Combine(ModListOutputFolder, "modlist.json"));
            //ModList.ToCERAS(Path.Combine(ModListOutputFolder, "modlist"), ref CerasConfig.Config);

            if(File.Exists(ModListOutputFile))
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
                }
            }
            Utils.Log("Removing ModList staging folder");
            //Directory.Delete(ModListOutputFolder, true);
        }

        private void GatherArchives()
        {
            Info("Building a list of archives based on the files required");

            var shas = InstallDirectives.OfType<FromArchive>()
                .Select(a => a.ArchiveHashPath[0])
                .Distinct();

            var archives = IndexedArchives.OrderByDescending(f => f.File.LastModified)
                .GroupBy(f => f.File.Hash)
                .ToDictionary(f => f.Key, f => f.First());

            SelectedArchives = shas.PMap(sha => ResolveArchive(sha, archives));
        }

        // TODO: this whole thing
        private Archive ResolveArchive(string sha, IDictionary<string, IndexedArchive> archives)
        {
            if (archives.TryGetValue(sha, out var found))
            {
                var result = new Archive();

                result.Name = found.Name;
                result.Hash = found.File.Hash;
                result.Size = found.File.Size;

                return result;
            }

            Error($"No match found for Archive sha: {sha} this shouldn't happen");
            return null;
        }

        public override Directive RunStack(IEnumerable<ICompilationStep> stack, RawSourceFile source)
        {
            Utils.Status($"Compiling {source.Path}");
            foreach (var step in stack)
            {
                var result = step.Run(source);
                if (result != null) return result;
            }

            throw new InvalidDataException("Data fell out of the compilation stack");

        }

        public override IEnumerable<ICompilationStep> GetStack()
        {
            var userConfig = Path.Combine(VortexFolder, "compilation_stack.yml");
            if (File.Exists(userConfig))
                return Serialization.Deserialize(File.ReadAllText(userConfig), this);

            IEnumerable<ICompilationStep> stack = MakeStack();

            File.WriteAllText(Path.Combine(VortexFolder, "_current_compilation_stack.yml"),
                Serialization.Serialize(stack));

            return stack;
        }

        public override IEnumerable<ICompilationStep> MakeStack()
        {
            Utils.Log("Generating compilation stack");
            return new List<ICompilationStep>
            {
                //new IncludePropertyFiles(this),

                new IgnoreGameFiles(this),
                new IncludeRegex(this, @".*\.zip?$"),
                //new IncludeRegex(this, "*.zip"),
                //new IgnoreStartsWith(this, Path.Combine(Consts.GameFolderFilesDir, "Data")),
                //new IgnoreStartsWith(this, Path.Combine(Consts.GameFolderFilesDir, "Papyrus Compiler")),
                //new IgnoreStartsWith(this, Path.Combine(Consts.GameFolderFilesDir, "Skyrim")),
                //new IgnoreRegex(this, Consts.GameFolderFilesDir + "\\\\.*\\.bsa"),

                new DirectMatch(this),

                new IgnoreGameFiles(this),

                new IgnoreWabbajackInstallCruft(this),

                new DropAll(this)
            };
        }
    }
}
