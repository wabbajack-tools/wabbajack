using Compression.BSA;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Wabbajack.Common;

namespace VirtualFileSystem
{
    public class VirtualFileSystem
    {

        internal static string _stagedRoot;
        public static VirtualFileSystem VFS;
        private Dictionary<string, VirtualFile> _files = new Dictionary<string, VirtualFile>();


        public static string RootFolder { get; }
        public Dictionary<string, IEnumerable<VirtualFile>> HashIndex { get; private set; }

        static VirtualFileSystem()
        {
            VFS = new VirtualFileSystem();
            RootFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _stagedRoot = Path.Combine(RootFolder, "vfs_staged_files");
            if (Directory.Exists(_stagedRoot))
                Directory.Delete(_stagedRoot, true);

            Directory.CreateDirectory(_stagedRoot);
           
        }

        public VirtualFileSystem ()
        {
            LoadFromDisk();
        }

        private void LoadFromDisk()
        {
            Utils.Log("Loading VFS Cache");
            if (!File.Exists("vfs_cache.bson")) return;
            _files = "vfs_cache.bson".FromBSON<IEnumerable<VirtualFile>>(root_is_array:true).ToDictionary(f => f.FullPath);
            CleanDB();
        }

        public void SyncToDisk()
        {
            lock(this)
            {
                _files.Values.OfType<VirtualFile>().ToBSON("vfs_cache.bson");
            }
        }


        public void Purge(VirtualFile f)
        {
            var path = f.FullPath + "|";
            lock (this)
            {
                _files.Values
                      .Where(v => v.FullPath.StartsWith(path) || v.FullPath == f.FullPath)
                      .ToList()
                      .Do(r => {
                          _files.Remove(r.FullPath);
                          });
            }
        }

        public void Add(VirtualFile f)
        {
            lock (this)
            {
                if (_files.ContainsKey(f.FullPath))
                    Purge(f);
                _files.Add(f.FullPath, f);
            }
        }

        public VirtualFile Lookup(string f)
        {
            lock (this)
            {
                if (_files.TryGetValue(f, out var found))
                    return found;
                return null;
            }
        }

        /// <summary>
        /// Remove any orphaned files in the DB.
        /// </summary>
        private void CleanDB()
        {
            Utils.Log("Cleaning VFS cache");
            lock (this)
            {
                _files.Values
                     .Where(f =>
                     {
                         if (f.IsConcrete)
                             return !File.Exists(f.StagedPath);
                         while (f.ParentPath != null)
                         {
                             if (Lookup(f.ParentPath) == null)
                                 return true;
                             f = Lookup(f.ParentPath);
                         }
                         return false;
                     })
                     .ToList()
                     .Do(f => _files.Remove(f.FullPath));
            }
        }

        /// <summary>
        /// Adds the root path to the filesystem. This may take quite some time as every file in the folder will be hashed,
        /// and every archive examined.
        /// </summary>
        /// <param name="path"></param>
        public void AddRoot(string path)
        {
            IndexPath(path);
            RefreshIndexes();
        }

        private void RefreshIndexes()
        {
            Utils.Log("Building Hash Index");
            lock(this)
            {
                HashIndex = _files.Values
                                  .GroupBy(f => f.Hash)
                                  .ToDictionary(f => f.Key, f => (IEnumerable<VirtualFile>)f);
            }
        }

        private void IndexPath(string path)
        {
            Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .PMap(f => UpdateFile(f));
            SyncToDisk();
        }

        private void UpdateFile(string f)
        {
        TOP:
            var lv = Lookup(f);
            if (lv == null)
            {
                Utils.Log($"Analyzing {0}");

                lv = new VirtualFile()
                {
                    Paths = new string[] { f }
                };

                lv.Analyze();
                Add(lv);
                if (lv.IsArchive)
                {
                    UpdateArchive(lv);
                }
                // Upsert after extraction incase extraction fails
            }
            if (lv.IsOutdated)
            {
                Purge(lv);
                goto TOP;
            }
        }

        private void UpdateArchive(VirtualFile f)
        {
            var entries = GetArchiveEntryNames(f);
            var new_files = entries.Select(e => {
                var new_path = new string[f.Paths.Length + 1];
                f.Paths.CopyTo(new_path, 0);
                new_path[f.Paths.Length] = e;
                var nf = new VirtualFile()
                {
                    Paths = new_path,
                };
                Add(nf);
                return nf;
                }).ToList();

            // Stage the files in the archive
            Stage(new_files);
            // Analyze them
            new_files.Do(file => file.Analyze());
            // Recurse into any archives in this archive
            new_files.Where(file => file.IsArchive).Do(file => UpdateArchive(file));
            // Unstage the file
            new_files.Where(file => file.IsStaged).Do(file => file.Unstage());

            f.FinishedIndexing = true;
            SyncToDisk();

            Utils.Log($"{_files.Count} docs in VFS cache");
        }

        private void Stage(IEnumerable<VirtualFile> files)
        {
            var grouped = files.GroupBy(f => f.ParentArchive)
                               .OrderBy(f => f.Key == null ? 0 : f.Key.Paths.Length)
                               .ToList();

            foreach (var group in grouped)
            {
                var indexed = group.ToDictionary(e => e.Paths[group.Key.Paths.Length]);
                FileExtractor.Extract(group.Key.StagedPath, e =>
                {
                    if (indexed.TryGetValue(e.Name, out var file))
                    {
                        return File.OpenWrite(file.GenerateStagedName());
                    }
                    return null;
                });
            }
        }

        public VirtualFile this[string path]
        {
            get
            {
                return Lookup(path);
            }
        }

        internal List<string> GetArchiveEntryNames(VirtualFile file)
        {
            if (!file.IsStaged)
                throw new InvalidDataException("File is not staged");

            if (file.Extension == ".bsa") {
                using (var ar = new BSAReader(file.StagedPath))
                {
                    return ar.Files.Select(f => f.Path).ToList();
                }
            }
            if (file.Extension == ".zip")
            {
                using (var s = new ZipFile(File.OpenRead(file.StagedPath)))
                {
                    s.IsStreamOwner = true;
                    s.UseZip64 = UseZip64.On;

                    if (s.OfType<ZipEntry>().FirstOrDefault(e => !e.CanDecompress) == null)
                    {
                        return s.OfType<ZipEntry>()
                                .Where(f => f.IsFile)
                                .Select(f => f.Name.Replace('/', '\\'))
                                .ToList();
                    }
                }
            }
            
            using (var e = new ArchiveFile(file.StagedPath))
            {
                return e.Entries
                        .Where(f => !f.IsFolder)
                        .Select(f => f.FileName).ToList();
            }

        }
    }


    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class VirtualFile
    {
        [JsonProperty]
        public string[] Paths { get; set; }
        [JsonProperty]
        public string Hash { get; set; }
        [JsonProperty]
        public long Size { get; set; }
        [JsonProperty]
        public ulong LastModified { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? FinishedIndexing { get; set; }


        private string _fullPath;

        public VirtualFile()
        {
        }

        private string _stagedPath;


        public string FullPath
        {
            get
            {
                if (_fullPath != null) return _fullPath;
                _fullPath = String.Join("|", Paths);
                return _fullPath;
            }
        }

        public string Extension
        {
            get
            {
                return Path.GetExtension(Paths.Last());
            }
        }

     


        /// <summary>
        /// If this file is in an archive, return the Archive File, otherwise return null.
        /// </summary>
        public VirtualFile TopLevelArchive
        {
            get
            {
                if (Paths.Length == 0) return null;
                return VirtualFileSystem.VFS[Paths[0]];
            }
        }

        public VirtualFile ParentArchive
        {
            get
            {
                if (ParentPath == null) return null;
                return VirtualFileSystem.VFS.Lookup(ParentPath);
            }
        }

        private bool? _isArchive;
        public bool IsArchive
        {
            get
            {
                if (_isArchive == null)
                    _isArchive = FileExtractor.CanExtract(Extension);
                return (bool)_isArchive;
            }
        }

        public bool IsStaged
        {
            get
            {
                if (IsConcrete) return true;
                return _stagedPath != null;
            }
        }

        public string StagedPath
        {
            get
            {
                if (!IsStaged)
                    throw new InvalidDataException("File is not staged");
                if (IsConcrete) return Paths[0];
                return _stagedPath;
            }
        }

        public FileStream OpenRead()
        {
            if (!IsStaged)
                throw new InvalidDataException("File is not staged, cannot open");
            return File.OpenRead(_stagedPath);
        }

        /// <summary>
        /// Calulate the file's SHA, size and last modified
        /// </summary>
        internal void Analyze()
        {
            if (!IsStaged)
                throw new InvalidDataException("Cannot analzye a unstaged file");

            var fio = new FileInfo(StagedPath);
            Size = fio.Length;
            Hash = Utils.FileSHA256(StagedPath);
            LastModified = fio.LastWriteTime.ToMilliseconds();
        }


        /// <summary>
        /// Delete the temoporary file associated with this file
        /// </summary>
        internal void Unstage()
        {
            if (IsStaged && !IsConcrete)
            {
                File.Delete(_stagedPath);
                _stagedPath = null;
            }
        }

        internal string GenerateStagedName()
        {
            _stagedPath = Path.Combine(VirtualFileSystem._stagedRoot, Guid.NewGuid().ToString() + Path.GetExtension(Paths.Last()));
            return _stagedPath;
        }

        /// <summary>
        /// Returns true if this file always exists on-disk, and doesn't need to be staged.
        /// </summary>
        public bool IsConcrete
        {
            get
            {
                return Paths.Length == 1;
            }
        }

        public bool IsOutdated
        {
            get
            {
                if (IsStaged)
                {
                    var fi = new FileInfo(StagedPath);
                    if (fi.LastWriteTime.ToMilliseconds() != LastModified || fi.Length != Size)
                        return true;
                    if (IsArchive)
                        if (!FinishedIndexing ?? true)
                            return true;
                }
                return false;
            }
                
        }

        private string _parentPath;
        public string ParentPath 
        {
          get {
                if (_parentPath == null && !IsConcrete)
                    _parentPath = String.Join("|", Paths.Take(Paths.Length - 1));
                return _parentPath;
          }
        }
    }


}
