using Compression.BSA;
using Newtonsoft.Json;
using SharpCompress.Archives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Wabbajack.Common;
using static Wabbajack.NexusAPI;

namespace Wabbajack
{
    public class Compiler
    {



        public string MO2Folder;

        public dynamic MO2Ini { get; }
        public string GamePath { get; }

        public bool IgnoreMissingFiles { get; set; }

        public string MO2DownloadsFolder
        {
            get
            {
                return Path.Combine(MO2Folder, "downloads");
            }
        }



        public string MO2Profile;

        public string MO2ProfileDir
        {
            get
            {
                return Path.Combine(MO2Folder, "profiles", MO2Profile);
            }
        }

        public Action<string> Log_Fn { get; }
        public List<Directive> InstallDirectives { get; private set; }
        public string NexusKey { get; private set; }
        internal UserStatus User { get; private set; }
        public List<Archive> SelectedArchives { get; private set; }
        public List<RawSourceFile> AllFiles { get; private set; }
        public ModList ModList { get; private set; }
        public ConcurrentBag<Directive> ExtraFiles { get; private set; }

        public List<IndexedArchive> IndexedArchives;

        public void Info(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = String.Format(msg, args);
            Log_Fn(msg);
        }

        public void Status(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = String.Format(msg, args);
            WorkQueue.Report(msg, 0);
        }


        private void Error(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = String.Format(msg, args);
            Log_Fn(msg);
            throw new Exception(msg);
        }

        public Compiler(string mo2_folder, Action<string> log_fn)
        {
            MO2Folder = mo2_folder;
            Log_Fn = log_fn;
            MO2Ini = Path.Combine(MO2Folder, "ModOrganizer.ini").LoadIniFile();
            GamePath = ((string)MO2Ini.General.gamePath).Replace("\\\\", "\\");
        }



        public void LoadArchives()
        {
            IndexedArchives = Directory.EnumerateFiles(MO2DownloadsFolder)
                               .Where(file => Consts.SupportedArchives.Contains(Path.GetExtension(file)))
                               .PMap(file => LoadArchive(file));
        }

        private IndexedArchive LoadArchive(string file)
        {
            string metaname = file + ".archive_contents";

            if (metaname.FileExists() && new FileInfo(metaname).LastWriteTime >= new FileInfo(file).LastWriteTime)
            {
                Status("Loading Archive Index for {0}", Path.GetFileName(file));
                var info = metaname.FromJSON<IndexedArchive>();
                info.Name = Path.GetFileName(file);
                info.AbsolutePath = file;

                var ini_name = file + ".meta";
                if (ini_name.FileExists())
                {
                    info.IniData = ini_name.LoadIniFile();
                    info.Meta = File.ReadAllText(ini_name);
                }

                return info;
            }

            Status("Indexing {0}", Path.GetFileName(file));
            var streams = new Dictionary<string, (SHA256Managed, long)>();
            FileExtractor.Extract(file, entry => {
                var sha = new SHA256Managed();
                var os = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
                streams.Add(entry.Name, (sha, (long)entry.Size));
                return os;
            });

            var indexed = new IndexedArchiveCache();
            indexed.Hash = file.FileSHA256();
            indexed.Entries = streams.Select(entry =>
            {
                return new IndexedEntry()
                {
                    Hash = entry.Value.Item1.Hash.ToBase64(),
                    Size = (long)entry.Value.Item2,
                    Path = entry.Key
                };
            }).ToList();

            streams.Do(e => e.Value.Item1.Dispose());

            indexed.ToJSON(metaname);
            return LoadArchive(file);
        }

        public void Compile()
        {
            var mo2_files = Directory.EnumerateFiles(MO2Folder, "*", SearchOption.AllDirectories)
                                     .Where(p => p.FileExists())
                                     .Select(p => new RawSourceFile() { Path = p.RelativeTo(MO2Folder), AbsolutePath = p });

            var game_files = Directory.EnumerateFiles(GamePath, "*", SearchOption.AllDirectories)
                                      .Where(p => p.FileExists())
                                      .Select(p => new RawSourceFile() { Path = Path.Combine(Consts.GameFolderFilesDir, p.RelativeTo(GamePath)), AbsolutePath = p });

            Info("Searching for mod files");
            AllFiles = mo2_files.Concat(game_files).ToList();

            Info("Found {0} files to build into mod list", AllFiles.Count);

            ExtraFiles = new ConcurrentBag<Directive>();

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
                else {
                    Info("Exiting due to no way to compile these files");
                    return;
                }
            }

            InstallDirectives = results.Where(i => !(i is IgnoredDirectly)).ToList();

            NexusKey = NexusAPI.GetNexusAPIKey();
            User = NexusAPI.GetUserStatus(NexusKey);
            
            if (!User.is_premium)
            {
                Info($"User {User.name} is not a premium Nexus user, cannot continue");
            }
           

            GatherArchives();
            BuildPatches();

            ModList = new ModList()
            {
                Archives = SelectedArchives,
                Directives = InstallDirectives,
                Name = MO2Profile
            };

            PatchExecutable();

            ResetMembers();

            Info("Done Building Modpack");
        }

        /// <summary>
        /// Clear references to lists that hold a lot of data.
        /// </summary>
        private void ResetMembers()
        {
            AllFiles = null;
            IndexedArchives = null;
            InstallDirectives = null;
            SelectedArchives = null;
            ExtraFiles = null;
        }


        /// <summary>
        /// Fills in the Patch fields in files that require them
        /// </summary>
        private void BuildPatches()
        {
            var groups = InstallDirectives.OfType<PatchedFromArchive>()
                                          .GroupBy(p => p.ArchiveHash)
                                          .ToList();

            Info("Patching building patches from {0} archives", groups.Count);
            var absolute_paths = AllFiles.ToDictionary(e => e.Path, e => e.AbsolutePath);
            groups.PMap(group => BuildArchivePatches(group.Key, group, absolute_paths));

            if (InstallDirectives.OfType<PatchedFromArchive>().FirstOrDefault(f => f.Patch == null) != null)
            {
                Error("Missing patches after generation, this should not happen");
            }

        }

        private void BuildArchivePatches(string archive_sha, IEnumerable<PatchedFromArchive> group, Dictionary<string, string> absolute_paths)
        {
            var archive = IndexedArchives.First(a => a.Hash == archive_sha);
            var paths = group.Select(g => g.From).ToHashSet();
            var streams = new Dictionary<string, MemoryStream>();
            Status($"Extracting {paths.Count} patch files from {archive.Name}");
            // First we fetch the source files from the input archive
            FileExtractor.Extract(archive.AbsolutePath, entry =>
            {
                if (!paths.Contains(entry.Name)) return null;

                var result = new MemoryStream();
                streams.Add(entry.Name, result);
                return result;
            }, false);

            /*
            using (var a = ArchiveFactory.Open(archive.AbsolutePath))
            {
                foreach (var entry in a.Entries)
                {
                    var path = entry.Key.Replace("/", "\\");
                    if (!paths.Contains(path)) continue;
                    var result = new MemoryStream();
                    streams.Add(path, result);
                    Info("Extracting {0}", path);
                    using (var stream = entry.OpenEntryStream())
                        stream.CopyTo(result);
                }
            }*/

            var extracted = streams.ToDictionary(k => k.Key, v => v.Value.ToArray());
            // Now Create the patches
            Status("Building Patches for {0}", archive.Name);
            Info("Building Patches for {0}", archive.Name);
            group.PMap(entry =>
            {
                Info("Patching {0}", entry.To);
                var ss = extracted[entry.From];
                using (var origin = new MemoryStream(ss))
                using (var output = new MemoryStream())
                {
                    var a = origin.ReadAll();
                    var b = LoadDataForTo(entry.To, absolute_paths);
                    BSDiff.Create(a, b, output);
                    entry.Patch = output.ToArray().ToBase64();
                }
            });

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
            var archives = IndexedArchives.GroupBy(a => a.Hash).ToDictionary(k => k.Key, k => k.First());

            var shas = InstallDirectives.OfType<FromArchive>()
                                        .Select(a => a.ArchiveHash)
                                        .Distinct();

            SelectedArchives = shas.PMap(sha => ResolveArchive(sha, archives));

        }

        private Archive ResolveArchive(string sha, Dictionary<string, IndexedArchive> archives)
        {
            if (archives.TryGetValue(sha, out var found))
            {
                if (found.IniData == null)
                    Error("No download metadata found for {0}, please use MO2 to query info or add a .meta file and try again.", found.Name);
                var general = found.IniData.General;
                if (general == null)
                    Error("No General section in mod metadata found for {0}, please use MO2 to query info or add the info and try again.", found.Name);

                Archive result;

                if (general.modID != null && general.fileID != null && general.gameName != null)
                {
                    result = new NexusMod()
                    {
                        GameName = general.gameName,
                        FileID = general.fileID,
                        ModID = general.modID
                    };
                    Status($"Getting Nexus info for {found.Name}");
                    try
                    {
                        var info = NexusAPI.GetFileInfo((NexusMod)result, NexusKey);
                    }
                    catch (Exception ex)
                    {
                        Error($"Unable to resolve {found.Name} on the Nexus was the file removed?");
                    }

                }
                else if (general.directURL != null && general.directURL.StartsWith("https://drive.google.com"))
                {
                    var regex = new Regex("((?<=id=)[a-zA-Z0-9_-]*)|(?<=\\/file\\/d\\/)[a-zA-Z0-9_-]*");
                    var match = regex.Match(general.directURL);
                    result = new GoogleDriveMod()
                    {
                        Id = match.ToString()
                    };
                }
                else if (general.directURL != null && general.directURL.StartsWith(Consts.MegaPrefix))
                {
                    result = new MEGAArchive()
                    {
                        URL = general.directURL
                    };
                }
                else if (general.directURL != null && general.directURL.StartsWith("https://www.dropbox.com/"))
                {
                    var uri = new UriBuilder((string)general.directURL);
                    var query = HttpUtility.ParseQueryString(uri.Query);

                    if (query.GetValues("dl").Count() > 0)
                        query.Remove("dl");

                    query.Set("dl", "1");

                    uri.Query = query.ToString();

                    result = new DirectURLArchive()
                    {
                        URL = uri.ToString()
                    };
                }
                else if (general.directURL != null && general.directURL.StartsWith("https://www.moddb.com/downloads/start"))
                {
                    result = new MODDBArchive()
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
                    
                    var tmp = new DirectURLArchive()
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
                else
                {
                    Error("No way to handle archive {0} but it's required by the modpack", found.Name);
                    return null;
                }

                result.Name = found.Name;
                result.Hash = found.Hash;
                result.Meta = found.Meta;

                return result;
            }
            Error("No match found for Archive sha: {0} this shouldn't happen", sha);
            return null;
        }


        private Directive RunStack(IEnumerable<Func<RawSourceFile, Directive>> stack, RawSourceFile source)
        {
            Status("Compiling {0}", source.Path);
            return (from f in stack
                    let result = f(source)
                    where result != null
                    select result).First();
        }


        /// <summary>
        /// Creates a execution stack. The stack should be passed into Run stack. Each function
        /// in this stack will be run in-order and the first to return a non-null result will have its
        /// result included into the pack
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Func<RawSourceFile, Directive>> MakeStack()
        {
            Info("Generating compilation stack");
            return new List<Func<RawSourceFile, Directive>>()
            {
                IgnoreStartsWith("logs\\"),
                IgnoreStartsWith("downloads\\"),
                IgnoreStartsWith("webcache\\"),
                IgnoreStartsWith("overwrite\\"),
                IgnoreEndsWith(".pyc"),
                IgnoreOtherProfiles(),
                IgnoreDisabledMods(),
                IncludeThisProfile(),
                // Ignore the ModOrganizer.ini file it contains info created by MO2 on startup
                IgnoreStartsWith("ModOrganizer.ini"),
                IgnoreStartsWith(Path.Combine(Consts.GameFolderFilesDir, "Data")),
                IgnoreStartsWith(Path.Combine(Consts.GameFolderFilesDir, "Papyrus Compiler")),
                IgnoreStartsWith(Path.Combine(Consts.GameFolderFilesDir, "Skyrim")),
                IgnoreRegex(Consts.GameFolderFilesDir + "\\\\.*\\.bsa"),
                IncludeModIniData(),
                DirectMatch(),
                IncludePatches(),

                DeconstructBSAs(),

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
                DropAll()
            };
        }


        /// <summary>
        /// This function will search for a way to create a BSA in the installed mod list by assembling it from files
        /// found in archives. To do this we hash all the files in side the BSA then try to find matches and patches for
        /// all of the files.
        /// </summary>
        /// <returns></returns>
        private Func<RawSourceFile, Directive> DeconstructBSAs()
        {
            var microstack = new List<Func<RawSourceFile, Directive>>()
            {
                DirectMatch(),
                IncludePatches(),
                DropAll()
            };

            return source =>
            {
                if (!Consts.SupportedBSAs.Contains(Path.GetExtension(source.Path))) return null;

                var hashed = HashBSA(source.AbsolutePath);

                var source_files = hashed.Select(e => new RawSourceFile() {
                    Hash = e.Item2,
                    Path = e.Item1,
                    AbsolutePath = e.Item1
                });


                var matches = source_files.Select(e => RunStack(microstack, e));

                var id = Guid.NewGuid().ToString();

                foreach (var match in matches)
                {
                    if (match is IgnoredDirectly)
                    {
                        Error($"File required for BSA creation doesn't exist: {match.To}");
                    }
                    match.To = Path.Combine(Consts.BSACreationDir, id, match.To);
                    ExtraFiles.Add(match);
                };

                CreateBSA directive;
                using (var bsa = new BSAReader(source.AbsolutePath))
                {
                    directive = new CreateBSA()
                    {
                        To = source.Path,
                        TempID = id,
                        Type = (uint)bsa.HeaderType,
                        FileFlags = (uint)bsa.FileFlags,
                        ArchiveFlags = (uint)bsa.ArchiveFlags,
                    };
                };

                return directive;

            };
        }

        /// <summary>
        /// Given a BSA on disk, index it and return a dictionary of SHA256 -> filename
        /// </summary>
        /// <param name="absolutePath"></param>
        /// <returns></returns>
        private List<(string, string)> HashBSA(string absolutePath)
        {
            Status($"Hashing BSA: {absolutePath}");
            var results = new List<(string, string)>();
            using (var a = new BSAReader(absolutePath))
            {
                foreach (var entry in a.Files)
                {
                    Status($"Hashing BSA: {absolutePath} - {entry.Path}");

                    var data = entry.GetData();
                    results.Add((entry.Path, data.SHA256()));
                }
            }
            return results;
        }

        private Func<RawSourceFile, Directive> IgnoreDisabledMods()
        {
            var disabled_mods = File.ReadAllLines(Path.Combine(MO2ProfileDir, "modlist.txt"))
                                    .Where(line => line.StartsWith("-") && !line.EndsWith("_separator"))
                                    .Select(line => Path.Combine("mods", line.Substring(1)) + "\\")
                                    .ToList();
            return source =>
            {
                if (disabled_mods.FirstOrDefault(mod => source.Path.StartsWith(mod)) != null)
                {
                    var r = source.EvolveTo<IgnoredDirectly>();
                    r.Reason = "Disabled Mod";
                    return r;
                }
                return null;
            };
        }

        private Func<RawSourceFile, Directive> IncludePatches()
        {
            var indexed = (from archive in IndexedArchives
                           from entry in archive.Entries
                           select new { archive = archive, entry = entry })
                           .GroupBy(e => Path.GetFileName(e.entry.Path).ToLower())
                           .ToDictionary(e => e.Key);

            return source =>
            {
                if (indexed.TryGetValue(Path.GetFileName(source.Path.ToLower()), out var value))
                {
                    var found = value.First();

                    var e = source.EvolveTo<PatchedFromArchive>();
                    e.From = found.entry.Path;
                    e.ArchiveHash = found.archive.Hash;
                    e.To = source.Path;
                    return e;
                }
                return null;
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
            var correct_profile = Path.Combine("profiles", MO2Profile) + "\\";
            return source =>
            {
                if (source.Path.StartsWith(correct_profile))
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
            return Encoding.UTF8.GetBytes(String.Join("\r\n", lines));
        }

        private Func<RawSourceFile, Directive> IgnoreOtherProfiles()
        {
            var correct_profile = Path.Combine("profiles", MO2Profile) + "\\";
            return source =>
            {
                if (source.Path.StartsWith("profiles\\") && !source.Path.StartsWith(correct_profile))
                {
                    var c = source.EvolveTo<IgnoredDirectly>();
                    c.Reason = "File not for this profile";
                    return c;
                }
                return null;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreEndsWith(string v)
        {
            var reason = String.Format("Ignored because path ends with {0}", v);
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
            var reason = String.Format("Ignored because path matches regex {0}", p);
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

        private Func<RawSourceFile, Directive> DropAll()
        {
            return source => {
                var result = source.EvolveTo<NoMatch>();
                result.Reason = "No Match in Stack";
                Info($"No match for: {source.Path}");
                return result;
            };
        }

        private Func<RawSourceFile, Directive> DirectMatch()
        {
            var indexed = (from archive in IndexedArchives
                           from entry in archive.Entries
                           select new { archive = archive, entry = entry })
                           .GroupBy(e => e.entry.Hash)
                           .ToDictionary(e => e.Key);

            var mod_inis = Directory.EnumerateDirectories(Path.Combine(MO2Folder, "mods"))
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

            return source =>
            {
                if (indexed.TryGetValue(source.Hash, out var found))
                {
                    var result = source.EvolveTo<FromArchive>();

                    var match = found.Where(f => Path.GetFileName(f.entry.Path) == Path.GetFileName(source.Path))
                                     .OrderByDescending(f => new FileInfo(f.archive.AbsolutePath).LastWriteTime)
                                     .FirstOrDefault();

                    if (match == null)
                        match = found.OrderByDescending(f => new FileInfo(f.archive.AbsolutePath).LastWriteTime)
                                     .FirstOrDefault();

                    result.ArchiveHash = match.archive.Hash;
                    result.From = match.entry.Path;
                    return result;
                }
                return null;
            };
        }

        private Func<RawSourceFile, Directive> IgnoreStartsWith(string v)
        {
            var reason = String.Format("Ignored because path starts with {0}", v);
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
            var settings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto };
            var data = JsonConvert.SerializeObject(ModList, settings).BZip2String();
            var executable = Assembly.GetExecutingAssembly().Location;
            var out_path = Path.Combine(Path.GetDirectoryName(executable), MO2Profile + ".exe");
            Info("Patching Executable {0}", Path.GetFileName(out_path));
            File.Copy(executable, out_path, true);
            using (var os = File.OpenWrite(out_path))
            using (var bw = new BinaryWriter(os))
            {
                long orig_pos = os.Length;
                os.Position = os.Length;
                bw.Write(data.LongLength);
                bw.Write(data);
                bw.Write(orig_pos);
                bw.Write(Encoding.ASCII.GetBytes(Consts.ModPackMagic));
            }
        }
    }
}
