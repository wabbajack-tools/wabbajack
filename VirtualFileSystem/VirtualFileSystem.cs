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

namespace VFS
{
    public class VirtualFileSystem
    {

        internal static string _stagedRoot;
        public static VirtualFileSystem VFS;
        private Dictionary<string, VirtualFile> _files = new Dictionary<string, VirtualFile>();
        private bool _disableDiskCache;

        public static string RootFolder { get; }
        public Dictionary<string, IEnumerable<VirtualFile>> HashIndex { get; private set; }

        static VirtualFileSystem()
        {
            VFS = new VirtualFileSystem();
            RootFolder = ".\\";
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
            try
            {
                Utils.Log("Loading VFS Cache");
                if (!File.Exists("vfs_cache.bson")) return;
                _files = "vfs_cache.bson".FromBSON<IEnumerable<VirtualFile>>(root_is_array: true).ToDictionary(f => f.FullPath);
                CleanDB();
            }
            catch(Exception ex)
            {
                Utils.Log($"Purging cache due to {ex}");
                File.Delete("vfs_cache.bson");
                _files.Clear();
            }
        }

        public void SyncToDisk()
        {
            if (!_disableDiskCache)
            lock(this)
            {
                _files.Values.OfType<VirtualFile>().ToBSON("vfs_cache.bson");
            }
        }

        public IList<VirtualFile> FilesInArchive(VirtualFile f)
        {
            var path = f.FullPath + "|";
            return _files.Values
                            .Where(v => v.FullPath.StartsWith(path))
                            .ToList();
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
                         if (f.Hash == null)
                             return true;
                         while (f.ParentPath != null)
                         {
                             if (Lookup(f.ParentPath) == null)
                                 return true;
                             if (f.Hash == null)
                                 return true;
                             f = Lookup(f.ParentPath);
                         }
                         return false;
                     })
                     .ToList()
                     .Do(f => _files.Remove(f.FullPath));
            }
        }

        public void BackfillMissing()
        {
            lock(this)
            {
                _files.Values
                      .Select(f => f.ParentPath)
                      .Where(s => s != null)
                      .Where(s => !_files.ContainsKey(s))
                      .ToHashSet()
                      .Do(s =>
                      {
                          AddKnown(new VirtualFile() { Paths = s.Split('|') });
                      });
            }
        }

        /// <summary>
        /// Add a known file to the index, bit of a hack as we won't assume that all the fields for the archive are filled in. 
        /// you will need to manually update the SHA hash when you are done adding files, by calling `RefreshIndexes`
        /// </summary>
        /// <param name="virtualFile"></param>
        public void AddKnown(VirtualFile virtualFile)
        {
            lock(this)
            {
                // We don't know enough about these files to be able to store them in the disk cache 
                _disableDiskCache = true;
                _files[virtualFile.FullPath] = virtualFile;
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

        public void RefreshIndexes()
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
            var file_list = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList();
            Utils.Log($"Updating the cache for {file_list.Count} files");
            file_list.PMap(f => UpdateFile(f));
            SyncToDisk();
        }

        private void UpdateFile(string f)
        {
        TOP:
            var lv = Lookup(f);
            if (lv == null)
            {
                Utils.Status($"Analyzing {f}");

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
        }

        public void Stage(IEnumerable<VirtualFile> files)
        {
            var grouped = files.SelectMany(f => f.FilesInPath)
                               .Distinct()
                               .Where(f => f.ParentArchive != null)
                               .GroupBy(f => f.ParentArchive)
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

        

        public StagingGroup StageWith(IEnumerable<VirtualFile> files)
        {
            var grp = new StagingGroup(files);
            grp.Stage();
            return grp;
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

        /// <summary>
        /// Given a path that starts with a HASH, return the Virtual file referenced
        /// </summary>
        /// <param name="archiveHashPath"></param>
        /// <returns></returns>
        public VirtualFile FileForArchiveHashPath(string[] archiveHashPath)
        {
            var archive = HashIndex[archiveHashPath[0]].Where(a => a.IsArchive).OrderByDescending(a => a.LastModified).First();
            string fullPath = archive.FullPath + "|" + String.Join("|", archiveHashPath.Skip(1));
            return Lookup(fullPath);
        }
    }

    public class StagingGroup : List<VirtualFile>, IDisposable
    {
        public StagingGroup(IEnumerable<VirtualFile> files) : base(files)
        {
        }

        public void Dispose()
        {
            this.Do(f => f.Unstage());
        }

        internal void Stage()
        {
            VirtualFileSystem.VFS.Stage(this);
        }
    }

    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class VirtualFile
    {
        public string[] _paths;
        [JsonProperty]
        public string[] Paths
        {
            get
            {
                return _paths;
            }
            set
            {
                for (int idx = 0; idx < value.Length; idx += 1)
                    value[idx] = String.Intern(value[idx]);
                _paths = value;
            }
        }
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
            set
            {
                if (IsStaged && value != null)
                    throw new InvalidDataException("Can't change the path of a already staged file");
                _stagedPath = value;
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
            if (_stagedPath != null) return _stagedPath;
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

        public IEnumerable<VirtualFile> FileInArchive
        {
            get
            {
                return VirtualFileSystem.VFS.FilesInArchive(this);
            }
        }

        public string[] MakeRelativePaths()
        {
            var path_copy = (string[])Paths.Clone();
            path_copy[0] = VirtualFileSystem.VFS.Lookup(Paths[0]).Hash;
            return path_copy;
        }

        public IEnumerable<VirtualFile> FilesInPath
        {
            get {
                return Enumerable.Range(1, Paths.Length)
                                 .Select(i => Paths.Take(i))
                                 .Select(path => VirtualFileSystem.VFS.Lookup(String.Join("|", path)));
                                    
            }
        }
    }


}
