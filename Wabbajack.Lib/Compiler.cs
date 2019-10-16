using CommonMark;
using Compression.BSA;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
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
        public string ModListOutputFile => MO2Profile + Consts.ModlistExtension;

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

            ModList.ToJSON(Path.Combine(ModListOutputFolder, "modlist.json"));

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


        private Directive RunStack(IEnumerable<Func<RawSourceFile, Directive>> stack, RawSourceFile source)
        {
            Status($"Compiling {source.Path}");
            foreach (var f in stack)
            {
                var result = f(source);
                if (result != null) return result;
            }

            throw new InvalidDataException("Data fell out of the compilation stack");
        }


        /// <summary>
        ///     Creates a execution stack. The stack should be passed into Run stack. Each function
        ///     in this stack will be run in-order and the first to return a non-null result will have its
        ///     result included into the pack
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Func<RawSourceFile, Directive>> MakeStack()
        {
            Info("Generating compilation stack");
            return new List<Func<RawSourceFile, Directive>>
            {
                IncludePropertyFiles(),
                IgnoreStartsWith("logs\\"),
                IncludeRegex("^downloads\\\\.*\\.meta"),
                IgnoreStartsWith("downloads\\"),
                IgnoreStartsWith("webcache\\"),
                IgnoreStartsWith("overwrite\\"),
                IgnorePathContains("temporary_logs"),
                IgnorePathContains("GPUCache"),
                IgnorePathContains("SSEEdit Cache"),
                IgnoreEndsWith(".pyc"),
                IgnoreEndsWith(".log"),
                IgnoreOtherProfiles(),
                IgnoreDisabledMods(),
                IncludeThisProfile(),
                // Ignore the ModOrganizer.ini file it contains info created by MO2 on startup
                IncludeStubbedConfigFiles(),
                IncludeLootFiles(),
                IgnoreStartsWith(Path.Combine(Consts.GameFolderFilesDir, "Data")),
                IgnoreStartsWith(Path.Combine(Consts.GameFolderFilesDir, "Papyrus Compiler")),
                IgnoreStartsWith(Path.Combine(Consts.GameFolderFilesDir, "Skyrim")),
                IgnoreRegex(Consts.GameFolderFilesDir + "\\\\.*\\.bsa"),
                IncludeModIniData(),
                DirectMatch(),
                IncludeTaggedFiles(Consts.WABBAJACK_INCLUDE),
                DeconstructBSAs(), // Deconstruct BSAs before building patches so we don't generate massive patch files
                IncludePatches(),
                IncludeDummyESPs(),


                // If we have no match at this point for a game folder file, skip them, we can't do anything about them
                IgnoreGameFiles(),

                // There are some types of files that will error the compilation, because they're created on-the-fly via tools
                // so if we don't have a match by this point, just drop them.
                IgnoreEndsWith(".ini"),
                IgnoreEndsWith(".html"),
                IgnoreEndsWith(".txt"),
                // Don't know why, but this seems to get copied around a bit
                IgnoreEndsWith("HavokBehaviorPostProcess.exe"),
                // Theme file MO2 downloads somehow
                IgnoreEndsWith("splash.png"),

                IgnoreEndsWith(".bin"),
                IgnoreEndsWith(".refcache"),

                IgnoreWabbajackInstallCruft(),

                PatchStockESMs(),

                IncludeAllConfigs(),
                IncludeTaggedFiles(Consts.WABBAJACK_NOMATCH_INCLUDE),
                zEditIntegration.IncludezEditPatches(this),

                DropAll()
            };
        }

        private Func<RawSourceFile, Directive> IncludePropertyFiles()
        {
            return source =>
            {
                var files = new HashSet<string>
                {
                    ModListImage, ModListReadme
                };
                if (!files.Any(f => source.AbsolutePath.Equals(f))) return null;
                if (!File.Exists(source.AbsolutePath)) return null;
                var isBanner = source.AbsolutePath == ModListImage;
                //var isReadme = source.AbsolutePath == ModListReadme;
                var result = source.EvolveTo<PropertyFile>();
                result.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                if (isBanner)
                {
                    result.Type = PropertyType.Banner;
                    ModListImage = result.SourceDataID;
                }
                else
                {
                    result.Type = PropertyType.Readme;
                    ModListReadme = result.SourceDataID;
                }
                return result;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreWabbajackInstallCruft()
        {
            var cruft_files = new HashSet<string>
            {
                "7z.dll", "7z.exe", "vfs_staged_files\\", "nexus.key_cache", "patch_cache\\",
                Consts.NexusCacheDirectory + "\\"
            };
            return source =>
            {
                if (!cruft_files.Any(f => source.Path.StartsWith(f))) return null;
                var result = source.EvolveTo<IgnoredDirectly>();
                result.Reason = "Wabbajack Cruft file";
                return result;
            };
        }

        private Func<RawSourceFile, Directive> IncludeAllConfigs()
        {
            return source =>
            {
                if (!Consts.ConfigFileExtensions.Contains(Path.GetExtension(source.Path))) return null;
                var result = source.EvolveTo<InlineFile>();
                result.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                return result;
            };
        }

        private Func<RawSourceFile, Directive> PatchStockESMs()
        {
            return source =>
            {
                var filename = Path.GetFileName(source.Path);
                var game_file = Path.Combine(GamePath, "Data", filename);
                if (Consts.GameESMs.Contains(filename) && source.Path.StartsWith("mods\\") && File.Exists(game_file))
                {
                    Info(
                        $"A ESM named {filename} was found in a mod that shares a name with a core game ESMs, it is assumed this is a cleaned ESM and it will be binary patched.");
                    var result = source.EvolveTo<CleanedESM>();
                    result.SourceESMHash = VFS.Lookup(game_file).Hash;

                    Status($"Generating patch of {filename}");
                    using (var ms = new MemoryStream())
                    {
                        Utils.CreatePatch(File.ReadAllBytes(game_file), File.ReadAllBytes(source.AbsolutePath), ms);
                        var data = ms.ToArray();
                        result.SourceDataID = IncludeFile(data);
                        Info($"Generated a {data.Length} byte patch for {filename}");

                    }


                    return result;
                }

                return null;
            };
        }


        private Func<RawSourceFile, Directive> IncludeLootFiles()
        {
            var prefix = Consts.LOOTFolderFilesDir + "\\";
            return source =>
            {
                if (source.Path.StartsWith(prefix))
                {
                    var result = source.EvolveTo<InlineFile>();
                    result.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath).ToBase64());
                    return result;
                }

                return null;
            };
        }

        private Func<RawSourceFile, Directive> IncludeStubbedConfigFiles()
        {
            return source =>
            {
                if (Consts.ConfigFileExtensions.Contains(Path.GetExtension(source.Path)))
                    return RemapFile(source, GamePath);
                return null;
            };
        }

        private Directive RemapFile(RawSourceFile source, string gamePath)
        {
            var data = File.ReadAllText(source.AbsolutePath);
            var original_data = data;

            data = data.Replace(GamePath, Consts.GAME_PATH_MAGIC_BACK);
            data = data.Replace(GamePath.Replace("\\", "\\\\"), Consts.GAME_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(GamePath.Replace("\\", "/"), Consts.GAME_PATH_MAGIC_FORWARD);

            data = data.Replace(MO2Folder, Consts.MO2_PATH_MAGIC_BACK);
            data = data.Replace(MO2Folder.Replace("\\", "\\\\"), Consts.MO2_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(MO2Folder.Replace("\\", "/"), Consts.MO2_PATH_MAGIC_FORWARD);

            data = data.Replace(MO2DownloadsFolder, Consts.DOWNLOAD_PATH_MAGIC_BACK);
            data = data.Replace(MO2DownloadsFolder.Replace("\\", "\\\\"), Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(MO2DownloadsFolder.Replace("\\", "/"), Consts.DOWNLOAD_PATH_MAGIC_FORWARD);

            if (data == original_data)
                return null;
            var result = source.EvolveTo<RemappedInlineFile>();
            result.SourceDataID = IncludeFile(Encoding.UTF8.GetBytes(data));
            return result;
        }

        private Func<RawSourceFile, Directive> IgnorePathContains(string v)
        {
            v = $"\\{v.Trim('\\')}\\";
            var reason = $"Ignored because path contains {v}";
            return source =>
            {
                if (source.Path.Contains(v))
                {
                    var result = source.EvolveTo<IgnoredDirectly>();
                    result.Reason = reason;
                    return result;
                }

                return null;
            };
        }


        /// <summary>
        ///     If a user includes WABBAJACK_INCLUDE directly in the notes or comments of a mod, the contents of that
        ///     mod will be inlined into the installer. USE WISELY.
        /// </summary>
        /// <returns></returns>
        private Func<RawSourceFile, Directive> IncludeTaggedFiles(string tag)
        {
            var include_directly = ModInis.Where(kv =>
            {
                var general = kv.Value.General;
                if (general.notes != null && general.notes.Contains(tag))
                    return true;
                if (general.comments != null && general.comments.Contains(tag))
                    return true;
                return false;
            }).Select(kv => $"mods\\{kv.Key}\\");


            return source =>
            {
                if (source.Path.StartsWith("mods"))
                    foreach (var modpath in include_directly)
                        if (source.Path.StartsWith(modpath))
                        {
                            var result = source.EvolveTo<InlineFile>();
                            result.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                            return result;
                        }

                return null;
            };
        }


        /// <summary>
        ///     Some tools like the Cathedral Asset Optimizer will create dummy ESPs whos only existance is to make
        ///     sure a BSA with the same name is loaded. We don't have a good way to detect these, but if an ESP is
        ///     less than 100 bytes in size and shares a name with a BSA it's a pretty good chance that it's a dummy
        ///     and the contents are generated.
        /// </summary>
        /// <returns></returns>
        private Func<RawSourceFile, Directive> IncludeDummyESPs()
        {
            return source =>
            {
                if (Path.GetExtension(source.AbsolutePath) == ".esp" || Path.GetExtension(source.AbsolutePath) == ".esm")
                {
                    var bsa = Path.Combine(Path.GetDirectoryName(source.AbsolutePath),
                        Path.GetFileNameWithoutExtension(source.AbsolutePath) + ".bsa");
                    var bsa_textures = Path.Combine(Path.GetDirectoryName(source.AbsolutePath),
                        Path.GetFileNameWithoutExtension(source.AbsolutePath) + " - Textures.bsa");
                    var esp_size = new FileInfo(source.AbsolutePath).Length;
                    if (esp_size <= 250 && (File.Exists(bsa) || File.Exists(bsa_textures)))
                    {
                        var inline = source.EvolveTo<InlineFile>();
                        inline.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                        return inline;
                    }
                }

                return null;
            };
        }


        /// <summary>
        ///     This function will search for a way to create a BSA in the installed mod list by assembling it from files
        ///     found in archives. To do this we hash all the files in side the BSA then try to find matches and patches for
        ///     all of the files.
        /// </summary>
        /// <returns></returns>
        private Func<RawSourceFile, Directive> DeconstructBSAs()
        {
            var include_directly = ModInis.Where(kv =>
            {
                var general = kv.Value.General;
                if (general.notes != null && general.notes.Contains(Consts.WABBAJACK_INCLUDE))
                    return true;
                if (general.comments != null && general.comments.Contains(Consts.WABBAJACK_INCLUDE))
                    return true;
                return false;
            }).Select(kv => $"mods\\{kv.Key}\\");

            var microstack = new List<Func<RawSourceFile, Directive>>
            {
                DirectMatch(),
                IncludePatches(),
                DropAll()
            };

            var microstack_with_include = new List<Func<RawSourceFile, Directive>>
            {
                DirectMatch(),
                IncludePatches(),
                IncludeALL()
            };


            return source =>
            {
                if (!Consts.SupportedBSAs.Contains(Path.GetExtension(source.Path).ToLower())) return null;

                var default_include = false;
                if (source.Path.StartsWith("mods"))
                    foreach (var modpath in include_directly)
                        if (source.Path.StartsWith(modpath))
                        {
                            default_include = true;
                            break;
                        }

                var source_files = source.File.FileInArchive;

                var stack = default_include ? microstack_with_include : microstack;

                var id = Guid.NewGuid().ToString();

                var matches = source_files.PMap(e => RunStack(stack, new RawSourceFile(e)
                {
                    Path = Path.Combine(Consts.BSACreationDir, id, e.Paths.Last())
                }));


                foreach (var match in matches)
                {
                    if (match is IgnoredDirectly) Error($"File required for BSA {source.Path} creation doesn't exist: {match.To}");
                    ExtraFiles.Add(match);
                }

                ;

                CreateBSA directive;
                using (var bsa = BSADispatch.OpenRead(source.AbsolutePath))
                {
                    directive = new CreateBSA
                    {
                        To = source.Path,
                        TempID = id,
                        State = bsa.State,
                        FileStates = bsa.Files.Select(f => f.State).ToList()
                    };
                }

                return directive;
            };
        }

        private Func<RawSourceFile, Directive> IncludeALL()
        {
            return source =>
            {
                var inline = source.EvolveTo<InlineFile>();
                inline.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                return inline;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreDisabledMods()
        {
            var always_enabled = ModInis.Where(f => IsAlwaysEnabled(f.Value)).Select(f => f.Key).ToHashSet();

            var all_enabled_mods = SelectedProfiles
                .SelectMany(p => File.ReadAllLines(Path.Combine(MO2Folder, "profiles", p, "modlist.txt")))
                .Where(line => line.StartsWith("+") || line.EndsWith("_separator"))
                .Select(line => line.Substring(1))
                .Concat(always_enabled)
                .Select(line => Path.Combine("mods", line) + "\\")
                .ToList();

            return source =>
            {
                if (!source.Path.StartsWith("mods") || all_enabled_mods.Any(mod => source.Path.StartsWith(mod)))
                    return null;
                var r = source.EvolveTo<IgnoredDirectly>();
                r.Reason = "Disabled Mod";
                return r;
            };
        }

        private static bool IsAlwaysEnabled(dynamic data)
        {
            if (data == null)
                return false;
            if (data.General != null && data.General.notes != null &&
                data.General.notes.Contains(Consts.WABBAJACK_ALWAYS_ENABLE))
                return true;
            if (data.General != null && data.General.comments != null &&
                data.General.notes.Contains(Consts.WABBAJACK_ALWAYS_ENABLE))
                return true;
            return false;
        }

        /// <summary>
        ///     This matches files based purely on filename, and then creates a binary patch.
        ///     In practice this is fine, because a single file tends to only come from one archive.
        /// </summary>
        /// <returns></returns>
        private Func<RawSourceFile, Directive> IncludePatches()
        {
            var indexed = IndexedFiles.Values
                .SelectMany(f => f)
                .GroupBy(f => Path.GetFileName(f.Paths.Last()).ToLower())
                .ToDictionary(f => f.Key);

            return source =>
            {
                if (!indexed.TryGetValue(Path.GetFileName(source.File.Paths.Last().ToLower()), out var value))
                    return null;

                var found = value.OrderByDescending(f => (f.TopLevelArchive ?? f).LastModified).First();

                var e = source.EvolveTo<PatchedFromArchive>();
                e.ArchiveHashPath = found.MakeRelativePaths();
                e.To = source.Path;
                e.Hash = source.File.Hash;

                Utils.TryGetPatch(found.Hash, source.File.Hash, out var data);

                if (data != null)
                    e.PatchID = IncludeFile(data);

                return e;
            };
        }

        private Func<RawSourceFile, Directive> IncludeModIniData()
        {
            return source =>
            {
                if (source.Path.StartsWith("mods\\") && source.Path.EndsWith("\\meta.ini"))
                {
                    var e = source.EvolveTo<InlineFile>();
                    e.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                    return e;
                }

                return null;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreGameFiles()
        {
            var start_dir = Consts.GameFolderFilesDir + "\\";
            return source =>
            {
                if (source.Path.StartsWith(start_dir))
                {
                    var i = source.EvolveTo<IgnoredDirectly>();
                    i.Reason = "Default game file";
                    return i;
                }

                return null;
            };
        }

        private Func<RawSourceFile, Directive> IncludeThisProfile()
        {
            var correct_profiles = SelectedProfiles.Select(p => Path.Combine("profiles", p) + "\\").ToList();

            return source =>
            {
                if (correct_profiles.Any(p => source.Path.StartsWith(p)))
                {
                    byte[] data;
                    if (source.Path.EndsWith("\\modlist.txt"))
                        data = ReadAndCleanModlist(source.AbsolutePath);
                    else
                        data = File.ReadAllBytes(source.AbsolutePath);

                    var e = source.EvolveTo<InlineFile>();
                    e.SourceDataID = IncludeFile(data);
                    return e;
                }

                return null;
            };
        }

        private byte[] ReadAndCleanModlist(string absolutePath)
        {
            var lines = File.ReadAllLines(absolutePath);
            lines = (from line in lines
                     where !(line.StartsWith("-") && !line.EndsWith("_separator"))
                     select line).ToArray();
            return Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
        }

        private Func<RawSourceFile, Directive> IgnoreOtherProfiles()
        {
            var profiles = SelectedProfiles
                .Select(p => Path.Combine("profiles", p) + "\\")
                .ToList();

            return source =>
            {
                if (source.Path.StartsWith("profiles\\"))
                {
                    if (profiles.Any(profile => source.Path.StartsWith(profile))) return null;
                    var c = source.EvolveTo<IgnoredDirectly>();
                    c.Reason = "File not for selected profiles";
                    return c;
                }

                return null;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreEndsWith(string v)
        {
            var reason = string.Format("Ignored because path ends with {0}", v);
            return source =>
            {
                if (source.Path.EndsWith(v))
                {
                    var result = source.EvolveTo<IgnoredDirectly>();
                    result.Reason = reason;
                    return result;
                }

                return null;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreRegex(string p)
        {
            var reason = string.Format("Ignored because path matches regex {0}", p);
            var regex = new Regex(p);
            return source =>
            {
                if (regex.IsMatch(source.Path))
                {
                    var result = source.EvolveTo<IgnoredDirectly>();
                    result.Reason = reason;
                    return result;
                }

                return null;
            };
        }


        private Func<RawSourceFile, Directive> IncludeRegex(string pattern)
        {
            var regex = new Regex(pattern);
            return source =>
            {
                if (regex.IsMatch(source.Path))
                {
                    var result = source.EvolveTo<InlineFile>();
                    result.SourceDataID = IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                    return result;
                }

                return null;
            };
        }


        private Func<RawSourceFile, Directive> DropAll()
        {
            return source =>
            {
                var result = source.EvolveTo<NoMatch>();
                result.Reason = "No Match in Stack";
                Info($"No match for: {source.Path}");
                return result;
            };
        }

        private Func<RawSourceFile, Directive> DirectMatch()
        {
            return source =>
            {
                if (IndexedFiles.TryGetValue(source.Hash, out var found))
                {
                    var result = source.EvolveTo<FromArchive>();

                    var match = found.Where(f =>
                            Path.GetFileName(f.Paths[f.Paths.Length - 1]) == Path.GetFileName(source.Path))
                        .OrderBy(f => f.Paths.Length)
                        .FirstOrDefault();

                    if (match == null)
                        match = found.OrderBy(f => f.Paths.Length).FirstOrDefault();

                    result.ArchiveHashPath = match.MakeRelativePaths();

                    return result;
                }

                return null;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreStartsWith(string v)
        {
            var reason = string.Format("Ignored because path starts with {0}", v);
            return source =>
            {
                if (source.Path.StartsWith(v))
                {
                    var result = source.EvolveTo<IgnoredDirectly>();
                    result.Reason = reason;
                    return result;
                }

                return null;
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