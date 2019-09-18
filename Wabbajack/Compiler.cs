using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using CommonMark;
using Compression.BSA;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using Newtonsoft.Json;
using VFS;
using Wabbajack.Common;
using static Wabbajack.NexusAPI;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack
{
    public class Compiler
    {
        private string _mo2DownloadsFolder;

        public Dictionary<string, IEnumerable<IndexedFileMatch>> DirectMatchIndex;


        public string MO2Folder;


        public string MO2Profile;

        public Compiler(string mo2_folder, Action<string> log_fn)
        {
            MO2Folder = mo2_folder;
            Log_Fn = log_fn;
            MO2Ini = Path.Combine(MO2Folder, "ModOrganizer.ini").LoadIniFile();
            GamePath = ((string) MO2Ini.General.gamePath).Replace("\\\\", "\\");
        }

        public dynamic MO2Ini { get; }
        public string GamePath { get; }

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

        public Action<string> Log_Fn { get; }
        public List<Directive> InstallDirectives { get; private set; }
        public string NexusKey { get; private set; }
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

        public void Info(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = string.Format(msg, args);
            Log_Fn(msg);
        }

        public void Status(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = string.Format(msg, args);
            WorkQueue.Report(msg, 0);
        }


        private void Error(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = string.Format(msg, args);
            Log_Fn(msg);
            throw new Exception(msg);
        }

        public void Compile()
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

            var mo2_files = Directory.EnumerateFiles(MO2Folder, "*", SearchOption.AllDirectories)
                .Where(p => p.FileExists())
                .Select(p => new RawSourceFile(VFS.Lookup(p)) {Path = p.RelativeTo(MO2Folder)});

            var game_files = Directory.EnumerateFiles(GamePath, "*", SearchOption.AllDirectories)
                .Where(p => p.FileExists())
                .Select(p => new RawSourceFile(VFS.Lookup(p))
                    {Path = Path.Combine(Consts.GameFolderFilesDir, p.RelativeTo(GamePath))});

            var loot_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LOOT");

            Info($"Indexing {loot_path}");
            VFS.AddRoot(loot_path);

            var loot_files = Directory.EnumerateFiles(loot_path, "userlist.yaml", SearchOption.AllDirectories)
                .Where(p => p.FileExists())
                .Select(p => new RawSourceFile(VFS.Lookup(p))
                    {Path = Path.Combine(Consts.LOOTFolderFilesDir, p.RelativeTo(loot_path))});


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
                .ToList();

            Info("Found {0} files to build into mod list", AllFiles.Count);

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
            Info("No match for {0} files", nomatch.Count());
            foreach (var file in nomatch)
                Info("     {0}", file.To);
            if (nomatch.Count() > 0)
            {
                if (IgnoreMissingFiles)
                {
                    Info("Continuing even though files were missing at the request of the user.");
                }
                else
                {
                    Info("Exiting due to no way to compile these files");
                    return;
                }
            }

            InstallDirectives = results.Where(i => !(i is IgnoredDirectly)).ToList();

            Info("Getting nexus api_key please click authorize if a browser window appears");

            NexusKey = GetNexusAPIKey();
            User = GetUserStatus(NexusKey);

            if (!User.is_premium) Info($"User {User.name} is not a premium Nexus user, cannot continue");


            GatherArchives();
            BuildPatches();

            ModList = new ModList
            {
                Archives = SelectedArchives,
                Directives = InstallDirectives,
                Name = MO2Profile
            };

            GenerateReport();
            PatchExecutable();

            ResetMembers();

            ShowReport();

            Info("Done Building Modpack");
        }

        private void ShowReport()
        {
            var file = Path.GetTempFileName() + ".html";
            File.WriteAllText(file, ModList.ReportHTML);
            Process.Start(file);
        }

        private void GenerateReport()
        {
            using (var fs = File.OpenWrite($"{ModList.Name}.md"))
            {
                fs.SetLength(0);
                using (var reporter = new ReportBuilder(fs))
                {
                    reporter.Build(ModList);
                }
            }

            ModList.ReportHTML = CommonMarkConverter.Convert(File.ReadAllText($"{ModList.Name}.md"));
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
                .Where(p => p.Patch == null)
                .GroupBy(p => p.ArchiveHashPath[0])
                .ToList();

            Info("Patching building patches from {0} archives", groups.Count);
            var absolute_paths = AllFiles.ToDictionary(e => e.Path, e => e.AbsolutePath);
            groups.PMap(group => BuildArchivePatches(group.Key, group, absolute_paths));

            if (InstallDirectives.OfType<PatchedFromArchive>().FirstOrDefault(f => f.Patch == null) != null)
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
                    Info("Patching {0}", entry.To);
                    using (var origin = by_path[string.Join("|", entry.ArchiveHashPath.Skip(1))].OpenRead())
                    using (var output = new MemoryStream())
                    {
                        var a = origin.ReadAll();
                        var b = LoadDataForTo(entry.To, absolute_paths);
                        Utils.CreatePatch(a, b, output);
                        entry.Patch = output.ToArray();
                        Info($"Patch size {entry.Patch.Length} for {entry.To}");
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

                using (var a = new BSAReader(Path.Combine(MO2Folder, bsa.To)))
                {
                    var file = a.Files.First(e => e.Path == Path.Combine(to.Split('\\').Skip(2).ToArray()));
                    return file.GetData();
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
                    Error(
                        "No download metadata found for {0}, please use MO2 to query info or add a .meta file and try again.",
                        found.Name);
                var general = found.IniData.General;
                if (general == null)
                    Error(
                        "No General section in mod metadata found for {0}, please use MO2 to query info or add the info and try again.",
                        found.Name);

                Archive result;

                if (general.directURL != null && general.directURL.StartsWith("https://drive.google.com"))
                {
                    var regex = new Regex("((?<=id=)[a-zA-Z0-9_-]*)|(?<=\\/file\\/d\\/)[a-zA-Z0-9_-]*");
                    var match = regex.Match(general.directURL);
                    result = new GoogleDriveMod
                    {
                        Id = match.ToString()
                    };
                }
                else if (general.directURL != null && general.directURL.StartsWith(Consts.MegaPrefix))
                {
                    result = new MEGAArchive
                    {
                        URL = general.directURL
                    };
                }
                else if (general.directURL != null && general.directURL.StartsWith("https://www.dropbox.com/"))
                {
                    var uri = new UriBuilder((string) general.directURL);
                    var query = HttpUtility.ParseQueryString(uri.Query);

                    if (query.GetValues("dl").Count() > 0)
                        query.Remove("dl");

                    query.Set("dl", "1");

                    uri.Query = query.ToString();

                    result = new DirectURLArchive
                    {
                        URL = uri.ToString()
                    };
                }
                else if (general.directURL != null &&
                         general.directURL.StartsWith("https://www.moddb.com/downloads/start"))
                {
                    result = new MODDBArchive
                    {
                        URL = general.directURL
                    };
                }
                else if (general.directURL != null && general.directURL.StartsWith("http://www.mediafire.com/file/"))
                {
                    Error("Mediafire links are not currently supported");
                    return null;
                    /*result = new MediaFireArchive()
                    {
                        URL = general.directURL
                    };*/
                }
                else if (general.directURL != null)
                {
                    var tmp = new DirectURLArchive
                    {
                        URL = general.directURL
                    };
                    if (general.directURLHeaders != null)
                    {
                        tmp.Headers = new List<string>();
                        tmp.Headers.AddRange(general.directURLHeaders.Split('|'));
                    }

                    result = tmp;
                }
                else if (general.manualURL != null)
                {
                    result = new ManualURLArchive
                    {
                        URL = general.manualURL.ToString()
                    };
                }
                else if (general.modID != null && general.fileID != null && general.gameName != null)
                {
                    var nm = new NexusMod
                    {
                        GameName = general.gameName,
                        FileID = general.fileID,
                        ModID = general.modID,
                        Version = general.version ?? "0.0.0.0"
                    };
                    var info = GetModInfo(nm, NexusKey);
                    nm.Author = info.author;
                    nm.UploadedBy = info.uploaded_by;
                    nm.UploaderProfile = info.uploaded_users_profile_url;
                    result = nm;
                }
                else
                {
                    Error("No way to handle archive {0} but it's required by the modlist", found.Name);
                    return null;
                }

                result.Name = found.Name;
                result.Hash = found.File.Hash;
                result.Meta = found.Meta;
                result.Size = found.File.Size;

                Info($"Checking link for {found.Name}");

                var installer = new Installer(null, "", Utils.Log);
                installer.NexusAPIKey = NexusKey;
                if (!installer.DownloadArchive(result, false))
                    Error(
                        $"Unable to resolve link for {found.Name}. If this is hosted on the nexus the file may have been removed.");

                return result;
            }

            Error("No match found for Archive sha: {0} this shouldn't happen", sha);
            return null;
        }


        private Directive RunStack(IEnumerable<Func<RawSourceFile, Directive>> stack, RawSourceFile source)
        {
            Status("Compiling {0}", source.Path);
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
                IncludeTaggedFiles(),
                DeconstructBSAs(), // Deconstruct BSAs before building patches so we don't generate massive patch files
                IncludePatches(),
                IncludeDummyESPs(),


                // If we have no match at this point for a game folder file, skip them, we can't do anything about them
                IgnoreGameFiles(),

                // There are some types of files that will error the compilation, because tehy're created on-the-fly via tools
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

                DropAll()
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
                result.SourceData = File.ReadAllBytes(source.AbsolutePath).ToBase64();
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
                        result.SourceData = ms.ToArray().ToBase64();
                    }

                    Info($"Generated a {result.SourceData.Length} byte patch for {filename}");

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
                    result.SourceData = File.ReadAllBytes(source.AbsolutePath).ToBase64();
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
            result.SourceData = Encoding.UTF8.GetBytes(data).ToBase64();
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
        private Func<RawSourceFile, Directive> IncludeTaggedFiles()
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


            return source =>
            {
                if (source.Path.StartsWith("mods"))
                    foreach (var modpath in include_directly)
                        if (source.Path.StartsWith(modpath))
                        {
                            var result = source.EvolveTo<InlineFile>();
                            result.SourceData = File.ReadAllBytes(source.AbsolutePath).ToBase64();
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
                if (Path.GetExtension(source.AbsolutePath) == ".esp")
                {
                    var bsa = Path.Combine(Path.GetDirectoryName(source.AbsolutePath),
                        Path.GetFileNameWithoutExtension(source.AbsolutePath) + ".bsa");
                    var bsa_textures = Path.Combine(Path.GetDirectoryName(source.AbsolutePath),
                        Path.GetFileNameWithoutExtension(source.AbsolutePath) + " - Textures.bsa");
                    var esp_size = new FileInfo(source.AbsolutePath).Length;
                    if (esp_size <= 100 && (File.Exists(bsa) || File.Exists(bsa_textures)))
                    {
                        var inline = source.EvolveTo<InlineFile>();
                        inline.SourceData = File.ReadAllBytes(source.AbsolutePath).ToBase64();
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
                if (!Consts.SupportedBSAs.Contains(Path.GetExtension(source.Path))) return null;

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
                    if (match is IgnoredDirectly) Error($"File required for BSA creation doesn't exist: {match.To}");
                    ExtraFiles.Add(match);
                }

                ;

                CreateBSA directive;
                using (var bsa = new BSAReader(source.AbsolutePath))
                {
                    directive = new CreateBSA
                    {
                        To = source.Path,
                        TempID = id,
                        Type = (uint) bsa.HeaderType,
                        FileFlags = (uint) bsa.FileFlags,
                        ArchiveFlags = (uint) bsa.ArchiveFlags
                    };
                }

                ;

                return directive;
            };
        }

        private Func<RawSourceFile, Directive> IncludeALL()
        {
            return source =>
            {
                var inline = source.EvolveTo<InlineFile>();
                inline.SourceData = File.ReadAllBytes(source.AbsolutePath).ToBase64();
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
                Utils.TryGetPatch(found.Hash, source.File.Hash, out e.Patch);
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
                    e.SourceData = File.ReadAllBytes(source.AbsolutePath).ToBase64();
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
                    e.SourceData = data.ToBase64();
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
                    result.SourceData = File.ReadAllBytes(source.AbsolutePath).ToBase64();
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

        internal void PatchExecutable()
        {
            Utils.Log("Exporting Installer");
            var settings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto};
            var executable = Assembly.GetExecutingAssembly().Location;
            var out_path = Path.Combine(Path.GetDirectoryName(executable), MO2Profile + ".exe");
            Info("Patching Executable {0}", Path.GetFileName(out_path));
            File.Copy(executable, out_path, true);
            using (var os = File.OpenWrite(out_path))
            using (var bw = new BinaryWriter(os))
            {
                var orig_pos = os.Length;
                os.Position = os.Length;
                //using (var compressor = new BZip2OutputStream(bw.BaseStream))
                /*using (var sw = new StreamWriter(compressor))
                using (var writer = new JsonTextWriter(sw))
                {
                    var serializer = new JsonSerializer();
                    serializer.TypeNameHandling = TypeNameHandling.Auto;
                    serializer.Serialize(writer, ModList);
                }*/
                //bw.Write(data);
                var formatter = new BinaryFormatter();

                using (var compressed = LZ4Stream.Encode(bw.BaseStream,
                    new LZ4EncoderSettings {CompressionLevel = LZ4Level.L10_OPT}, true))
                {
                    formatter.Serialize(compressed, ModList);
                }

                bw.Write(orig_pos);
                bw.Write(Encoding.ASCII.GetBytes(Consts.ModListMagic));
            }
        }

        public class IndexedFileMatch
        {
            public IndexedArchive Archive;
            public IndexedArchiveEntry Entry;
            public DateTime LastModified;
        }
    }
}