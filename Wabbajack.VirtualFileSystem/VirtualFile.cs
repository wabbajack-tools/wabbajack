using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using K4os.Hash.Crc;
using RocksDbSharp;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public class VirtualFile
    {
        private static RocksDb _vfsCache;


        static VirtualFile()
        {
            var options = new DbOptions().SetCreateIfMissing(true);
            _vfsCache = RocksDb.Open(options, (string)Consts.LocalAppDataPath.Combine("GlobalVFSCache2.rocksDb"));
        }
        
        private AbsolutePath _stagedPath;

        private IEnumerable<VirtualFile> _thisAndAllChildren;

        public IPath Name { get; internal set; }

        public RelativePath RelativeName => (RelativePath)Name;

        public AbsolutePath AbsoluteName => (AbsolutePath)Name;


        public FullPath FullPath { get; private set; }

        public Hash Hash { get; internal set; }
        
        public ExtendedHashes ExtendedHashes { get; set; }
        public long Size { get; internal set; }

        public ulong LastModified { get; internal set; }

        public ulong LastAnalyzed { get; internal set; }

        public VirtualFile Parent { get; internal set; }

        public Context Context { get; set; }

        private IExtractedFile _stagedFile = null;
        public IExtractedFile StagedFile  
        {
            get
            {
                if (IsNative) return new ExtractedDiskFile(AbsoluteName);
                if (_stagedFile == null)
                    throw new InvalidDataException("File is unstaged");
                return _stagedFile;
            }
            set
            {
                _stagedFile = value;
            }
            
        }

        /// <summary>
        ///     Returns the nesting factor for this file. Native files will have a nesting of 1, the factor
        ///     goes up for each nesting of a file in an archive.
        /// </summary>
        public int NestingFactor
        {
            get
            {
                var cnt = 0;
                var cur = this;
                while (cur != null)
                {
                    cnt += 1;
                    cur = cur.Parent;
                }

                return cnt;
            }
        }

        public ImmutableList<VirtualFile> Children { get; internal set; } = ImmutableList<VirtualFile>.Empty;

        public bool IsArchive => Children != null && Children.Count > 0;

        public bool IsNative => Parent == null;

        public IEnumerable<VirtualFile> ThisAndAllChildren
        {
            get
            {
                if (_thisAndAllChildren == null)
                {
                    _thisAndAllChildren = Children.SelectMany(child => child.ThisAndAllChildren).Append(this).ToList();
                }

                return _thisAndAllChildren;
            }
        }


        /// <summary>
        ///     Returns all the virtual files in the path to this file, starting from the root file.
        /// </summary>
        public IEnumerable<VirtualFile> FilesInFullPath
        {
            get
            {
                var stack = ImmutableStack<VirtualFile>.Empty;
                var cur = this;
                while (cur != null)
                {
                    stack = stack.Push(cur);
                    cur = cur.Parent;
                }

                return stack;
            }
        }


        public VirtualFile TopParent => IsNative ? this : Parent.TopParent;


        public T ThisAndAllChildrenReduced<T>(T acc, Func<T, VirtualFile, T> fn)
        {
            acc = fn(acc, this);
            return Children.Aggregate(acc, (current, itm) => itm.ThisAndAllChildrenReduced(current, fn));
        }

        public void ThisAndAllChildrenReduced(Action<VirtualFile> fn)
        {
            fn(this);
            foreach (var itm in Children)
                itm.ThisAndAllChildrenReduced(fn);
        }
        
        private static VirtualFile ConvertFromIndexedFile(Context context, IndexedVirtualFile file, IPath path, VirtualFile vparent, IExtractedFile extractedFile)
        {
            var vself = new VirtualFile
            {
                Context = context,
                Name = path,
                Parent = vparent,
                Size = file.Size,
                LastModified = extractedFile.LastModifiedUtc.AsUnixTime(),
                LastAnalyzed = DateTime.Now.AsUnixTime(),
                Hash = file.Hash
            };
                        
            vself.FillFullPath();

            vself.Children = file.Children.Select(f => ConvertFromIndexedFile(context, f, f.Name, vself, extractedFile)).ToImmutableList();

            return vself;
        }

        private static bool TryGetFromCache(Context context, VirtualFile parent, IPath path, IExtractedFile extractedFile, Hash hash, out VirtualFile found)
        {
            var result = _vfsCache.Get(hash.ToArray());
            if (result == null)
            {
                found = null;
                return false;
            }

            var data = IndexedVirtualFile.Read(new MemoryStream(result));
            found = ConvertFromIndexedFile(context, data, path, parent, extractedFile);
            found.Name = path;
            found.Hash = hash;
            return true;

        }

        private IndexedVirtualFile ToIndexedVirtualFile()
        {
            return new IndexedVirtualFile
            {
                Hash = Hash,
                Name = Name,
                Children = Children.Select(c => c.ToIndexedVirtualFile()).ToList(),
                Size = Size
            };
        }


        public static async Task<VirtualFile> Analyze(Context context, VirtualFile parent, IExtractedFile extractedFile,
            IPath relPath, int depth = 0)
        {
            var hash = await extractedFile.HashAsync();

            if (!context.UseExtendedHashes && FileExtractor.MightBeArchive(relPath.FileName.Extension))
            {
                // Disabled because it isn't enabled on the server
                IndexedVirtualFile result = null; //await TryGetContentsFromServer(hash);

                if (result != null)
                {
                    Utils.Log($"Downloaded VFS data for {relPath.FileName}");


                    return ConvertFromIndexedFile(context, result, relPath, parent, extractedFile);
                }
            }

            if (TryGetFromCache(context, parent, relPath, extractedFile, hash, out var vself))
                return vself;

            var self = new VirtualFile
            {
                Context = context,
                Name = relPath,
                Parent = parent,
                Size = extractedFile.Size,
                LastModified = extractedFile.LastModifiedUtc.AsUnixTime(),
                LastAnalyzed = DateTime.Now.AsUnixTime(),
                Hash = hash
            };

            self.FillFullPath(depth);
            
            if (context.UseExtendedHashes)
                self.ExtendedHashes = await ExtendedHashes.FromFile(extractedFile);

            if (!await extractedFile.CanExtract()) return self;

            try
            {

                await using var extracted = await extractedFile.ExtractAll(context.Queue);

                var list = await extracted
                    .PMap(context.Queue,
                        file => Analyze(context, self, file.Value, file.Key, depth + 1));

                self.Children = list.ToImmutableList();
            }
            catch (Exception ex)
            {
                Utils.Log($"Error while examining the contents of {relPath.FileName}");
                throw;
            }

            await using var ms = new MemoryStream();
            self.ToIndexedVirtualFile().Write(ms);
            _vfsCache.Put(self.Hash.ToArray(), ms.ToArray());
            
            return self;
        }

        internal void FillFullPath()
        {
            int depth = 0;
            var self = this;
            while (self.Parent != null)
            {
                depth += 1;
                self = self.Parent;
            }

            FillFullPath(depth);
        }
        
        internal void FillFullPath(int depth)
        {
            if (depth == 0)
            {
                FullPath = new FullPath((AbsolutePath)Name);
            }
            else
            {
                var paths = new RelativePath[depth];
                var self = this;
                for (var idx = depth; idx != 0; idx -= 1)
                {
                    paths[idx - 1] = self.RelativeName;
                    self = self.Parent;
                }
                FullPath = new FullPath(self.AbsoluteName, paths);
            }
            
        }

        private static async Task<IndexedVirtualFile> TryGetContentsFromServer(Hash hash)
        {
            try
            {
                var client = new HttpClient();
                var response =
                    await client.GetAsync($"http://{Consts.WabbajackCacheHostname}/indexed_files/{hash.ToHex()}");
                if (!response.IsSuccessStatusCode)
                    return null;

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    return stream.FromJson<IndexedVirtualFile>();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }


        public void Write(BinaryWriter bw)
        {
            bw.Write(Name);
            bw.Write(Size);
            bw.Write(LastModified);
            bw.Write(LastModified);
            bw.Write(Hash);
            bw.Write(Children.Count);
            foreach (var child in Children)
                child.Write(bw);
        }

        public static VirtualFile Read(Context context, byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            return Read(context, null, br);
        }

        private static VirtualFile Read(Context context, VirtualFile parent, BinaryReader br)
        {
            var vf = new VirtualFile
            {
                Name = br.ReadIPath(),
                Size = br.ReadInt64(),
                LastModified = br.ReadUInt64(),
                LastAnalyzed = br.ReadUInt64(),
                Hash = br.ReadHash(),
                Context = context,
                Parent = parent,
                Children = ImmutableList<VirtualFile>.Empty
            };
            vf.FullPath = new FullPath(vf.AbsoluteName, new RelativePath[0]);
            var children = br.ReadInt32();
            for (var i = 0; i < children; i++)
            {
                var child = Read(context, vf, br, (AbsolutePath)vf.Name, new RelativePath[0]);
                vf.Children = vf.Children.Add(child);
            }
            return vf;
        }
        
        private static VirtualFile Read(Context context, VirtualFile parent, BinaryReader br, AbsolutePath top, RelativePath[] subpaths)
        {
            var name = (RelativePath)br.ReadIPath();
            subpaths = subpaths.Add(name);
            var vf = new VirtualFile
            {
                Name = name,
                Size = br.ReadInt64(),
                LastModified = br.ReadUInt64(),
                LastAnalyzed = br.ReadUInt64(),
                Hash = br.ReadHash(),
                Context = context,
                Parent = parent,
                Children = ImmutableList<VirtualFile>.Empty,
                FullPath = new FullPath(top, subpaths)
            };

            var children = br.ReadInt32();
            for (var i = 0; i < children; i++)
            {
                var child = Read(context, vf, br,top, subpaths);
                vf.Children = vf.Children.Add(child);
            }
            return vf;
        }

        public HashRelativePath MakeRelativePaths()
        {
            var paths = new RelativePath[FilesInFullPath.Count() - 1];

            var idx = 0;
            foreach (var itm in FilesInFullPath.Skip(1))
            {
                paths[idx] = (RelativePath)itm.Name;
                idx += 1;
            }

            var path = new HashRelativePath(FilesInFullPath.First().Hash, paths);
            return path;
        }

        public async ValueTask<Stream> OpenRead()
        {
            return await StagedFile.OpenRead();
        }
    }

    public class ExtendedHashes
    {
        public string SHA256 { get; set; }
        public string SHA1 { get; set; }
        public string MD5 { get; set; }
        public string CRC { get; set; }

        public static async ValueTask<ExtendedHashes> FromFile(IExtractedFile file)
        {
            var hashes = new ExtendedHashes();
            await using var stream = await file.OpenRead();
            hashes.SHA256 = System.Security.Cryptography.SHA256.Create().ComputeHash(stream).ToHex();
            stream.Position = 0;
            hashes.SHA1 = System.Security.Cryptography.SHA1.Create().ComputeHash(stream).ToHex();
            stream.Position = 0;
            hashes.MD5 = System.Security.Cryptography.MD5.Create().ComputeHash(stream).ToHex();
            stream.Position = 0;

            var bytes = new byte[1024 * 8];
            var crc = new Crc32();
            while (true)
            {
                var read = stream.Read(bytes, 0, bytes.Length);
                if (read == 0) break;
                crc.Update(bytes, 0, read);
            }

            hashes.CRC = crc.DigestBytes().ToHex();

            return hashes;
        }
    }


    public class CannotStageNativeFile : Exception
    {
        public CannotStageNativeFile(string cannotStageANativeFile) : base(cannotStageANativeFile)
        {
        }
    }

    public class UnstagedFileException : Exception
    {
        private readonly FullPath _fullPath;

        public UnstagedFileException(FullPath fullPath) : base($"File {fullPath} is unstaged, cannot get staged name")
        {
            _fullPath = fullPath;
        }
    }
}
