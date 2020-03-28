using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using K4os.Hash.Crc;
using MessagePack;
using Wabbajack.Common;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.VirtualFileSystem
{
    public class VirtualFile
    {
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

        public AbsolutePath StagedPath
        {
            get
            {
                if (IsNative)
                    return (AbsolutePath)Name;
                if (_stagedPath == null)
                    throw new UnstagedFileException(FullPath);
                return _stagedPath;
            }
            internal set
            {
                if (IsNative)
                    throw new CannotStageNativeFile("Cannot stage a native file");
                _stagedPath = value;
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

        public static async Task<VirtualFile> Analyze(Context context, VirtualFile parent, AbsolutePath absPath,
            IPath relPath, int depth = 0)
        {
            var hash = absPath.FileHash();

            if (!context.UseExtendedHashes && FileExtractor.MightBeArchive(absPath))
            {
                var result = await TryGetContentsFromServer(hash);

                if (result != null)
                {
                    Utils.Log($"Downloaded VFS data for {(string)absPath}");

                    VirtualFile Convert(IndexedVirtualFile file, IPath path, VirtualFile vparent)
                    {
                        var vself = new VirtualFile
                        {
                            Context = context,
                            Name = path,
                            Parent = vparent,
                            Size = file.Size,
                            LastModified = absPath.LastModifiedUtc.AsUnixTime(),
                            LastAnalyzed = DateTime.Now.AsUnixTime(),
                            Hash = file.Hash
                        };

                        vself.Children = file.Children.Select(f => Convert(f, f.Name, vself)).ToImmutableList();

                        return vself;
                    }

                    return Convert(result, relPath, parent);
                }
            }

            var self = new VirtualFile
            {
                Context = context,
                Name = relPath,
                Parent = parent,
                Size = absPath.Size,
                LastModified = absPath.LastModifiedUtc.AsUnixTime(),
                LastAnalyzed = DateTime.Now.AsUnixTime(),
                Hash = hash
            };

            self.FillFullPath(depth);
            
            if (context.UseExtendedHashes)
                self.ExtendedHashes = ExtendedHashes.FromFile(absPath);

            if (FileExtractor.CanExtract(absPath))
            {
                using (var tempFolder = Context.GetTemporaryFolder())
                {
                    await FileExtractor.ExtractAll(context.Queue, absPath, tempFolder.FullName);

                    var list = await tempFolder.FullName.EnumerateFiles()
                        .PMap(context.Queue,
                            absSrc => Analyze(context, self, absSrc, absSrc.RelativeTo(tempFolder.FullName), depth + 1));

                    self.Children = list.ToImmutableList();
                }
            }

            return self;
        }

        private void FillFullPath(in int depth)
        {
            if (depth == 0)
            {
                FullPath = new FullPath((AbsolutePath)Name, new RelativePath[0]);
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
                    return stream.FromJSON<IndexedVirtualFile>();
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

        public FileStream OpenRead()
        {
            return StagedPath.OpenRead();
        }
    }

    public class ExtendedHashes
    {
        public string SHA256 { get; set; }
        public string SHA1 { get; set; }
        public string MD5 { get; set; }
        public string CRC { get; set; }

        public static ExtendedHashes FromFile(AbsolutePath file)
        {
            var hashes = new ExtendedHashes();
            using (var stream = file.OpenRead())
            {
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
            }

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
