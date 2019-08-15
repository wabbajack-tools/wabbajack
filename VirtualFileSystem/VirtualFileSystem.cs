using Compression.BSA;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace VirtualFileSystem
{
    public class VirtualFileSystem
    {
        private Dictionary<string, VirtualFile> _files = new Dictionary<string, VirtualFile>();
        internal string _stagedRoot;

        public VirtualFileSystem()
        {
            _stagedRoot = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "vfs_staged_files");
            Directory.CreateDirectory(_stagedRoot);
        }

        /// <summary>
        /// Adds the root path to the filesystem. This may take quite some time as every file in the folder will be hashed,
        /// and every archive examined.
        /// </summary>
        /// <param name="path"></param>
        public void AddRoot(string path, Action<string> status)
        {
            IndexPath(path, status);
        }

        private void SyncToDisk()
        {
            lock (this)
            {
                _files.Values.ToList().ToJSON("vfs_cache.json");
            }
        }

        private void IndexPath(string path, Action<string> status)
        {
            Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .PMap(f => UpdateFile(f));
        }

        private void UpdateFile(string f)
        {
        TOP:
            Console.WriteLine(f);
            var lv = Lookup(f);
            if (lv == null)
            {
                lv = new VirtualFile(this)
                {
                    Paths = new string[] { f }
                };
                this[f] = lv;
                lv.Analyze();
                if (lv.IsArchive)
                {
                    UpdateArchive(lv);
                }
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
                var nf = new VirtualFile(this)
                {
                    Paths = new_path,
                };
                this[nf.FullPath] = nf;
                return nf;
                }).ToList();

            // Stage the files in the archive
            Stage(new_files);
            // Analyze them
            new_files.Do(file => file.Analyze());
            // Recurse into any archives in this archive
            new_files.Where(file => file.IsArchive).Do(file => UpdateArchive(f));
            // Unstage the file
            new_files.Where(file => file.IsStaged).Do(file => file.Unstage());

            SyncToDisk();

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

        internal VirtualFile Lookup(string path)
        {
            lock(this)
            {
                if (_files.TryGetValue(path, out VirtualFile value))
                    return value;
                return null;
            }
        }

        public VirtualFile this[string path]
        {
            get
            {
                return Lookup(path);
            }
            set
            {
                lock(this)
                {
                    _files[path] = value;
                }
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
        /// Remove all cached data for this file and if it is a top level archive, any sub-files.
        /// </summary>
        /// <param name="file"></param>
        internal void Purge(VirtualFile file)
        {
            lock(this)
            {
                // Remove the file
                _files.Remove(file.FullPath);

                // If required, remove sub-files
                if (file.IsArchive)
                {
                    string prefix = file.FullPath + "|";
                    _files.Where(f => f.Key.StartsWith(prefix)).ToList().Do(f => _files.Remove(f.Key));
                }
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class VirtualFile
    {
        [JsonProperty]
        public string[] Paths;
        [JsonProperty]
        public string Hash;
        [JsonProperty]
        public long Size;
        [JsonProperty]
        public DateTime LastModifiedUTC;

        private string _fullPath;
        private VirtualFileSystem _vfs;

        public VirtualFile(VirtualFileSystem vfs)
        {
            _vfs = vfs;
        }

        [JsonIgnore]
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
                return _vfs[Paths[0]];
            }
        }

        public VirtualFile ParentArchive
        {
            get
            {
                if (Paths.Length == 0) return null;
                return _vfs[String.Join("|", Paths.Take(Paths.Length - 1))];
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
            LastModifiedUTC = fio.LastWriteTimeUtc;
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
            _stagedPath = Path.Combine(_vfs._stagedRoot, Guid.NewGuid().ToString() + Path.GetExtension(Paths.Last()));
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
                    if (fi.LastWriteTimeUtc != LastModifiedUTC || fi.Length != Size)
                        return true;
                }
                return false;
            }
                
        }
    }


}
