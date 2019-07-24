using Newtonsoft.Json;
using SevenZipExtractor;
using System;
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

namespace Wabbajack
{
    public class Compiler
    {



        public string MO2Folder;

        public dynamic MO2Ini { get; }
        public string GamePath { get; }

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
        public List<Archive> SelectedArchives { get; private set; }
        public List<RawSourceFile> AllFiles { get; private set; }
        public ModList ModList { get; private set; }

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

            using (var ar = new ArchiveFile(file))
            {
                Status("Indexing {0}", Path.GetFileName(file));
                var streams = new Dictionary<string, (SHA256Managed, long)>();
                ar.Extract(entry => {
                    if (entry.IsFolder) return null;

                    var sha = new SHA256Managed();
                    var os = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
                    streams.Add(entry.FileName, (sha, (long)entry.Size));
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
        }

        public void Compile()
        {
            var mo2_files = Directory.EnumerateFiles(MO2Folder, "*", SearchOption.AllDirectories)
                                     .Where(p => p.FileExists())
                                     .Select(p => new RawSourceFile() { Path = p.RelativeTo(MO2Folder), AbsolutePath = p });

            var game_files = Directory.EnumerateFiles(GamePath, "*", SearchOption.AllDirectories)
                                      .Where(p => p.FileExists())
                                      .Select(p => new RawSourceFile() { Path = Path.Combine(Consts.GameFolderFilesDir, p.RelativeTo(GamePath)), AbsolutePath = p });

            AllFiles = mo2_files.Concat(game_files).ToList();

            Info("Found {0} files to build into mod list", AllFiles.Count);

            var stack = MakeStack();

            Info("Running Compilation Stack");
            var results = AllFiles.PMap(f => RunStack(stack, f)).ToList();

            var nomatch = results.OfType<NoMatch>();
            Info("No match for {0} files", nomatch.Count());
            foreach (var file in nomatch)
                Info("     {0}", file.To);

            InstallDirectives = results.Where(i => !(i is IgnoredDirectly)).ToList();

            GatherArchives();
            BuildPatches();

            ModList = new ModList()
            {
                Archives = SelectedArchives,
                Directives = InstallDirectives
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
            Status("Etracting Patch Files from {0}", archive.Name);
            // First we fetch the source files from the input archive
            using (var a = new ArchiveFile(archive.AbsolutePath))
            {
                a.Extract(entry =>
                {
                    if (!paths.Contains(entry.FileName)) return null;

                    var result = new MemoryStream();
                    streams.Add(entry.FileName, result);
                    return result;
                }, false);
            }

            var extracted = streams.ToDictionary(k => k.Key, v => v.Value.ToArray());
            // Now Create the patches
            Status("Building Patches for {0}", archive.Name);
            Parallel.ForEach(group, entry =>
            {
                Info("Patching {0}", entry.To);
                var ss = extracted[entry.From];
                using (var origin = new MemoryStream(ss))
                using (var dest = File.OpenRead(absolute_paths[entry.To]))
                using (var output = new MemoryStream())
                {
                    var a = origin.ReadAll();
                    var b = dest.ReadAll();
                    BSDiff.Create(a, b, output);
                    entry.Patch = output.ToArray().ToBase64();
                }
            });

        }

        private void GatherArchives()
        {
            var archives = IndexedArchives.GroupBy(a => a.Hash).ToDictionary(k => k.Key, k => k.First());

            var shas = InstallDirectives.OfType<FromArchive>()
                                        .Select(a => a.ArchiveHash)
                                        .Distinct();

            SelectedArchives = shas.Select(sha => ResolveArchive(sha, archives)).ToList();

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
                else if (general.directURL != null)
                {
                    result = new DirectURLArchive()
                    {
                        URL = general.directURL
                    };
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

                // If we have no match at this point for a game folder file, skip them, we can't do anything about them
                IgnoreGameFiles(),
                DropAll()
            };
        }

        private Func<RawSourceFile, Directive> IgnoreDisabledMods()
        {
            var disabled_mods = File.ReadAllLines(Path.Combine(MO2ProfileDir, "modlist.txt"))
                                    .Where(line => line.StartsWith("-") && !line.EndsWith("_separator"))
                                    .Select(line => Path.Combine("mods", line.Substring(1)))
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
                           .GroupBy(e => Path.GetFileName(e.entry.Path))
                           .ToDictionary(e => e.Key);

            return source =>
            {
                if (indexed.TryGetValue(Path.GetFileName(source.Path), out var value))
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

            return source =>
            {
                if (indexed.TryGetValue(source.Hash, out var found))
                {
                    var result = source.EvolveTo<FromArchive>();
                    var match = found.FirstOrDefault(f => Path.GetFileName(f.entry.Path) == Path.GetFileName(source.Path));
                    if (match == null)
                        match = found.First();

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
            var data = JsonConvert.SerializeObject(ModList).BZip2String();
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
