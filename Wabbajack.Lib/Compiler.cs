using CommonMark;
using Compression.BSA;
using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class Compiler
    {
        private string _mo2DownloadsFolder;

        public Dictionary<string, IEnumerable<IndexedFileMatch>> DirectMatchIndex;


        public string MO2Folder;


        public string MO2Profile;
        public string ModListName, ModListAuthor, ModListDescription, ModListWebsite, ModListImage, ModListReadme;
        public string WabbajackVersion;

        public Compiler(string mo2_folder)
        {
            MO2Folder = mo2_folder;
            MO2Ini = Path.Combine(MO2Folder, "ModOrganizer.ini").LoadIniFile();
            GamePath = ((string)MO2Ini.General.gamePath).Replace("\\\\", "\\");
        }

        public dynamic MO2Ini { get; }
        public string GamePath { get; }

        public bool ShowReportWhenFinished { get; set; } = true;

        public bool IgnoreMissingFiles { get; set; }

        public string MO2DownloadsFolder
        {
            get
            {
                if (_mo2DownloadsFolder != null) return _mo2DownloadsFolder;
                if (MO2Ini != null)
                    if (MO2Ini.Settings != null)
                        if (MO2Ini.Settings.download_directory != null)
                            return MO2Ini.Settings.download_directory.Replace("/", "\\");
                return Path.Combine(MO2Folder, "downloads");
            }
            set => _mo2DownloadsFolder = value;
        }

        public string MO2ProfileDir => Path.Combine(MO2Folder, "profiles", MO2Profile);

        public string ModListOutputFolder => "output_folder";
        public string ModListOutputFile => MO2Profile + ExtensionManager.Extension;

        public List<Directive> InstallDirectives { get; private set; }
        internal UserStatus User { get; private set; }
        public List<Archive> SelectedArchives { get; private set; }
        public List<RawSourceFile> AllFiles { get; private set; }
        public ModList ModList { get; private set; }
        public ConcurrentBag<Directive> ExtraFiles { get; private set; }
        public Dictionary<string, dynamic> ModInis { get; private set; }

        public VirtualFileSystem VFS => VirtualFileSystem.VFS;

        public List<IndexedArchive> IndexedArchives { get; private set; }
        public Dictionary<string, IEnumerable<VirtualFile>> IndexedFiles { get; private set; }

        public HashSet<string> SelectedProfiles { get; set; } = new HashSet<string>();

        public void Info(string msg)
        {
            Utils.Log(msg);
        }

        public void Status(string msg)
        {
            WorkQueue.Report(msg, 0);
        }

        private void Error(string msg)
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

        public bool Compile()
        {
            VirtualFileSystem.Clean();
            Info("Looking for other profiles");
            var other_profiles_path = Path.Combine(MO2ProfileDir, "otherprofiles.txt");
            SelectedProfiles = new HashSet<string>();
            if (File.Exists(other_profiles_path)) SelectedProfiles = File.ReadAllLines(other_profiles_path).ToHashSet();
            SelectedProfiles.Add(MO2Profile);

            Info("Using Profiles: " + string.Join(", ", SelectedProfiles.OrderBy(p => p)));

            Info($"Indexing {MO2Folder}");
            VFS.AddRoot(MO2Folder);
            Info($"Indexing {GamePath}");
            VFS.AddRoot(GamePath);

            Info($"Indexing {MO2DownloadsFolder}");
            VFS.AddRoot(MO2DownloadsFolder);

            Info("Cleaning output folder");
            if (Directory.Exists(ModListOutputFolder))
                Directory.Delete(ModListOutputFolder, true);

            Directory.CreateDirectory(ModListOutputFolder);

            var mo2_files = Directory.EnumerateFiles(MO2Folder, "*", SearchOption.AllDirectories)
                .Where(p => p.FileExists())
                .Select(p => new RawSourceFile(VFS.Lookup(p)) { Path = p.RelativeTo(MO2Folder) });

            var game_files = Directory.EnumerateFiles(GamePath, "*", SearchOption.AllDirectories)
                .Where(p => p.FileExists())
                .Select(p => new RawSourceFile(VFS.Lookup(p))
                { Path = Path.Combine(Consts.GameFolderFilesDir, p.RelativeTo(GamePath)) });

            var loot_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LOOT");

            // TODO: make this generic so we can add more paths
            IEnumerable<RawSourceFile> loot_files = new List<RawSourceFile>();
            if (Directory.Exists(loot_path))
            {
                Info($"Indexing {loot_path}");
                VFS.AddRoot(loot_path);

                loot_files = Directory.EnumerateFiles(loot_path, "userlist.yaml", SearchOption.AllDirectories)
                    .Where(p => p.FileExists())
                    .Select(p => new RawSourceFile(VFS.Lookup(p))
                    { Path = Path.Combine(Consts.LOOTFolderFilesDir, p.RelativeTo(loot_path)) });
            }




            Info("Indexing Archives");
            IndexedArchives = Directory.EnumerateFiles(MO2DownloadsFolder)
                .Where(f => File.Exists(f + ".meta"))
                .Select(f => new IndexedArchive
                {
                    File = VFS.Lookup(f),
                    Name = Path.GetFileName(f),
                    IniData = (f + ".meta").LoadIniFile(),
                    Meta = File.ReadAllText(f + ".meta")
                })
                .ToList();

            Info("Indexing Files");
            var grouped = VFS.GroupedByArchive();
            IndexedFiles = IndexedArchives.Select(f =>
                {
                    if (grouped.TryGetValue(f.File, out var result))
                        return result;
                    return new List<VirtualFile>();
                })
                .SelectMany(fs => fs)
                .Concat(IndexedArchives.Select(f => f.File))
                .OrderByDescending(f => f.TopLevelArchive.LastModified)
                .GroupBy(f => f.Hash)
                .ToDictionary(f => f.Key, f => f.AsEnumerable());

            Info("Searching for mod files");

            AllFiles = mo2_files.Concat(game_files)
                .Concat(loot_files)
                .DistinctBy(f => f.Path)
                .ToList();

            Info($"Found {AllFiles.Count} files to build into mod list");

            Info("Verifying destinations");
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

            ExtraFiles = new ConcurrentBag<Directive>();

            ModInis = Directory.EnumerateDirectories(Path.Combine(MO2Folder, "mods"))
                .Select(f =>
                {
                    var mod_name = Path.GetFileName(f);
                    var meta_path = Path.Combine(f, "meta.ini");
                    if (File.Exists(meta_path))
                        return (mod_name, meta_path.LoadIniFile());
                    return (null, null);
                })
                .Where(f => f.Item2 != null)
                .ToDictionary(f => f.Item1, f => f.Item2);

            var stack = MakeStack();


            Info("Running Compilation Stack");
            var results = AllFiles.PMap(f => RunStack(stack, f)).ToList();

            // Add the extra files that were generated by the stack
            Info($"Adding {ExtraFiles.Count} that were generated by the stack");
            results = results.Concat(ExtraFiles).ToList();

            var nomatch = results.OfType<NoMatch>();
            Info($"No match for {nomatch.Count()} files");
            foreach (var file in nomatch)
                Info($"     {file.To}");
            if (nomatch.Count() > 0)
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

            Info("Getting Nexus api_key, please click authorize if a browser window appears");

            if (IndexedArchives.Any(a => a.IniData?.General?.gameName != null))
            {
                var nexusClient = new NexusApiClient();
                if (!nexusClient.IsPremium) Error($"User {nexusClient.Username} is not a premium Nexus user, so we cannot access the necessary API calls, cannot continue");

            }

            zEditIntegration.VerifyMerges(this);

            GatherArchives();
            BuildPatches();

            ModList = new ModList
            {
                GameType = GameRegistry.Games.Values.First(f => f.MO2Name == MO2Ini.General.gameName).Game,
                WabbajackVersion = WabbajackVersion,
                Archives = SelectedArchives,
                Directives = InstallDirectives,
                Name = ModListName ?? MO2Profile,
                Author = ModListAuthor ?? "",
                Description = ModListDescription ?? "",
                Readme = ModListReadme ?? "",
                Image = ModListImage ?? "",
                Website = ModListWebsite ?? ""
            };

            ValidateModlist.RunValidation(ModList);

            GenerateReport();
            ExportModlist();

            ResetMembers();

            ShowReport();

            Info("Done Building Modlist");
            return true;
        }

        private void ExportModlist()
        {
            Utils.Log($"Exporting Modlist to : {ModListOutputFile}");

            //ModList.ToJSON(Path.Combine(ModListOutputFolder, "modlist.json"));
            ModList.ToCERAS(Path.Combine(ModListOutputFolder, "modlist"), ref CerasConfig.Config);

            if (File.Exists(ModListOutputFile))
                File.Delete(ModListOutputFile);

            using (var fs = new FileStream(ModListOutputFile, FileMode.Create))
            {
                using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    Directory.EnumerateFiles(ModListOutputFolder, "*.*")
                        .DoProgress("Compressing Modlist",
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
            Utils.Log("Removing modlist staging folder");
            Directory.Delete(ModListOutputFolder, true);



        }

        private void ShowReport()
        {
            if (!ShowReportWhenFinished) return;

            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, ModList.ReportHTML);
            Process.Start(file);
        }

        private void GenerateReport()
        {
            string css = "";
            using (Stream cssStream = Utils.GetResourceStream("Wabbajack.Lib.css-min.css"))
            {
                using (StreamReader reader = new StreamReader(cssStream))
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

        /// <summary>
        ///     Clear references to lists that hold a lot of data.
        /// </summary>
        private void ResetMembers()
        {
            AllFiles = null;
            InstallDirectives = null;
            SelectedArchives = null;
            ExtraFiles = null;
        }


        /// <summary>
        ///     Fills in the Patch fields in files that require them
        /// </summary>
        private void BuildPatches()
        {
            Info("Gathering patch files");
            var groups = InstallDirectives.OfType<PatchedFromArchive>()
                .Where(p => p.PatchID == null)
                .GroupBy(p => p.ArchiveHashPath[0])
                .ToList();

            Info($"Patching building patches from {groups.Count} archives");
            var absolute_paths = AllFiles.ToDictionary(e => e.Path, e => e.AbsolutePath);
            groups.PMap(group => BuildArchivePatches(group.Key, group, absolute_paths));

            if (InstallDirectives.OfType<PatchedFromArchive>().FirstOrDefault(f => f.PatchID == null) != null)
                Error("Missing patches after generation, this should not happen");
        }

        private void BuildArchivePatches(string archive_sha, IEnumerable<PatchedFromArchive> group,
            Dictionary<string, string> absolute_paths)
        {
            var archive = VFS.HashIndex[archive_sha];
            using (var files = VFS.StageWith(group.Select(g => VFS.FileForArchiveHashPath(g.ArchiveHashPath))))
            {
                var by_path = files.GroupBy(f => string.Join("|", f.Paths.Skip(1)))
                    .ToDictionary(f => f.Key, f => f.First());
                // Now Create the patches
                group.PMap(entry =>
                {
                    Info($"Patching {entry.To}");
                    Status($"Patching {entry.To}");
                    using (var origin = by_path[string.Join("|", entry.ArchiveHashPath.Skip(1))].OpenRead())
                    using (var output = new MemoryStream())
                    {
                        var a = origin.ReadAll();
                        var b = LoadDataForTo(entry.To, absolute_paths);
                        Utils.CreatePatch(a, b, output);
                        entry.PatchID = IncludeFile(output.ToArray());
                        var file_size = File.GetSize(Path.Combine(ModListOutputFolder, entry.PatchID));
                        Info($"Patch size {file_size} for {entry.To}");
                    }
                });
            }
        }

        private byte[] LoadDataForTo(string to, Dictionary<string, string> absolute_paths)
        {
            if (absolute_paths.TryGetValue(to, out var absolute))
                return File.ReadAllBytes(absolute);

            if (to.StartsWith(Consts.BSACreationDir))
            {
                var bsa_id = to.Split('\\')[1];
                var bsa = InstallDirectives.OfType<CreateBSA>().First(b => b.TempID == bsa_id);

                using (var a = BSADispatch.OpenRead(Path.Combine(MO2Folder, bsa.To)))
                {
                    var find = Path.Combine(to.Split('\\').Skip(2).ToArray());
                    var file = a.Files.First(e => e.Path.Replace('/', '\\') == find);
                    using (var ms = new MemoryStream())
                    {
                        file.CopyDataTo(ms);
                        return ms.ToArray();
                    }
                }
            }

            Error($"Couldn't load data for {to}");
            return null;
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

        private Archive ResolveArchive(string sha, IDictionary<string, IndexedArchive> archives)
        {
            if (archives.TryGetValue(sha, out var found))
            {
                if (found.IniData == null)
                    Error($"No download metadata found for {found.Name}, please use MO2 to query info or add a .meta file and try again.");

                var result = new Archive();
                result.State = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(found.IniData);

                if (result.State == null)
                    Error($"{found.Name} could not be handled by any of the downloaders");

                result.Name = found.Name;
                result.Hash = found.File.Hash;
                result.Meta = found.Meta;
                result.Size = found.File.Size;

                Info($"Checking link for {found.Name}");

                if (!result.State.Verify())
                    Error(
                        $"Unable to resolve link for {found.Name}. If this is hosted on the Nexus the file may have been removed.");

                return result;
            }

            Error($"No match found for Archive sha: {sha} this shouldn't happen");
            return null;
        }


        public static Directive RunStack(IEnumerable<ICompilationStep> stack, RawSourceFile source)
        {
            Utils.Status($"Compiling {source.Path}");
            foreach (var step in stack)
            {
                var result = step.Run(source);
                if (result != null) return result;
            }

            throw new InvalidDataException("Data fell out of the compilation stack");
        }

        public IEnumerable<ICompilationStep> GetStack()
        {
            var user_config = Path.Combine(MO2ProfileDir, "compilation_stack.yml");
            if (File.Exists(user_config))
                return Serialization.Deserialize(File.ReadAllText(user_config), this);

            var stack = MakeStack();

            File.WriteAllText(Path.Combine(MO2ProfileDir, "_current_compilation_stack.yml"), 
                Serialization.Serialize(stack));

            return stack;

        }

        /// <summary>
        ///     Creates a execution stack. The stack should be passed into Run stack. Each function
        ///     in this stack will be run in-order and the first to return a non-null result will have its
        ///     result included into the pack
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ICompilationStep> MakeStack()
        {
            Utils.Log("Generating compilation stack");
            return new List<ICompilationStep>
            {
                new IncludePropertyFiles(this),
                new IgnoreStartsWith(this,"logs\\"),
                new IncludeRegex(this, "^downloads\\\\.*\\.meta"),
                new IgnoreStartsWith(this, "downloads\\"),
                new IgnoreStartsWith(this,"webcache\\"),
                new IgnoreStartsWith(this, "overwrite\\"),
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
                new IgnoreStartsWith(this, Path.Combine(Consts.GameFolderFilesDir, "Data")),
                new IgnoreStartsWith(this, Path.Combine(Consts.GameFolderFilesDir, "Papyrus Compiler")),
                new IgnoreStartsWith(this, Path.Combine(Consts.GameFolderFilesDir, "Skyrim")),
                new IgnoreRegex(this, Consts.GameFolderFilesDir + "\\\\.*\\.bsa"),
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

        public class IndexedFileMatch
        {
            public IndexedArchive Archive;
            public IndexedArchiveEntry Entry;
            public DateTime LastModified;
        }
    }
}