using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using K4os.Hash.Crc;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.VirtualFileSystem
{
    public class VirtualFile
    {
        private FullPath _fullPath;

        private AbsolutePath _stagedPath;
        public AbstractPath Name { get; internal set; }

        public FullPath FullPath
        {
            get
            {
                if (_fullPath != null) return _fullPath;

                var cur = this;
                var acc = new LinkedList<AbstractPath>();
                while (cur != null)
                {
                    acc.AddFirst(cur.Name);
                    cur = cur.Parent;
                }

                _fullPath = new FullPath(acc.First() as AbsolutePath, acc.Skip(1).OfType<RelativePath>().ToArray());
                return _fullPath;
            }
        }

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
                    return Name as AbsolutePath;
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

        private IEnumerable<VirtualFile> _thisAndAllChildren = null;

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
        
        
        public T ThisAndAllChildrenReduced<T>(T acc, Func<T, VirtualFile, T> fn)
        {
            acc = fn(acc, this);
            return this.Children.Aggregate(acc, (current, itm) => itm.ThisAndAllChildrenReduced<T>(current, fn));
        }
        
        public void ThisAndAllChildrenReduced(Action<VirtualFile> fn)
        {
            fn(this);
            foreach (var itm in Children)
                itm.ThisAndAllChildrenReduced(fn);
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

        public static async Task<VirtualFile> Analyze(Context context, VirtualFile parent, AbsolutePath absPath,
            AbstractPath relPath, bool topLevel)
        {
            var hash = absPath.FileHash();

            if (!context.UseExtendedHashes && FileExtractor.MightBeArchive(absPath))
            {
                var result = await TryGetContentsFromServer(hash);

                if (result != null)
                {
                    Utils.Log($"Downloaded VFS data for {(string)absPath}");
                    VirtualFile Convert(IndexedVirtualFile file, AbstractPath path, VirtualFile vparent)
                    {
                        var vself = new VirtualFile
                        {
                            Context = context,
                            Name = path,
                            Parent = vparent,
                            Size = file.Size,
                            LastModified = absPath.LastModifiedUtc.AsUnixTime(),
                            LastAnalyzed = DateTime.Now.AsUnixTime(),
                            Hash = file.Hash,

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
            if (context.UseExtendedHashes)
                self.ExtendedHashes = ExtendedHashes.FromFile(absPath);

            if (FileExtractor.CanExtract(absPath))
            {

                using (var tempFolder = Context.GetTemporaryFolder())
                {
                    await FileExtractor.ExtractAll(context.Queue, absPath, tempFolder.FullName);

                    var list = await tempFolder.FullName.EnumerateFiles()
                                        .PMap(context.Queue, absSrc => Analyze(context, self, absSrc, absSrc.RelativeTo(tempFolder.FullName), false));

                    self.Children = list.ToImmutableList();
                }

            }

            return self;
        }

        private static async Task<IndexedVirtualFile> TryGetContentsFromServer(Hash hash)
        {
            try
            {
                var client = new HttpClient();
                var response = await client.GetAsync($"http://{Consts.WabbajackCacheHostname}/indexed_files/{hash.ToHex()}");
                if (!response.IsSuccessStatusCode)
                    return null;

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    return stream.FromJSON<IndexedVirtualFile>();
                }

            }
            catch (Exception ex)
            {
                return null;
            }
        }


        private void Write(Stream stream)
        {
            stream.WriteAsMessagePack(this);
        }

        public static VirtualFile Read(Context context, byte[] data)
        {
            using var ms = new MemoryStream(data);
            return Read(context, null, ms);
        }

        private static VirtualFile Read(Context context, VirtualFile parent, Stream br)
        {
            var vf = br.ReadAsMessagePack<VirtualFile>();
            vf.Parent = parent;
            vf.Context = context;
            vf.Children ??= ImmutableList<VirtualFile>.Empty;

            return vf;
        }

        public static VirtualFile CreateFromPortable(Context context,
            Dictionary<Hash, IEnumerable<PortableFile>> state, Dictionary<Hash, AbsolutePath> links,
            PortableFile portableFile)
        {
            var vf = new VirtualFile
            {
                Parent = null,
                Context = context,
                Name = links[portableFile.Hash],
                Hash = portableFile.Hash,
                Size = portableFile.Size
            };
            if (state.TryGetValue(portableFile.Hash, out var children))
                vf.Children = children.Select(child => CreateFromPortable(context, vf, state, child)).ToImmutableList();
            return vf;
        }

        public static VirtualFile CreateFromPortable(Context context, VirtualFile parent,
            Dictionary<Hash, IEnumerable<PortableFile>> state, PortableFile portableFile)
        {
            var vf = new VirtualFile
            {
                Parent = parent,
                Context = context,
                Name = portableFile.Name,
                Hash = portableFile.Hash,
                Size = portableFile.Size
            };
            if (state.TryGetValue(portableFile.Hash, out var children))
                vf.Children = children.Select(child => CreateFromPortable(context, vf, state, child)).ToImmutableList();
            return vf;
        }

        public string[] MakeRelativePaths()
        {
            var path = new string[NestingFactor];
            path[0] = FilesInFullPath.First().Hash.ToBase64();
            
            var idx = 1;

            foreach (var itm in FilesInFullPath.Skip(1))
            {
                path[idx] = itm.Name;
                idx += 1;
            }
            return path;
        }

        public FileStream OpenRead()
        {
            return StagedPath.OpenRead();
        }
    }

    public class ExtendedHashes
    {
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

        public string SHA256 { get; set; }
        public string SHA1 { get; set; }
        public string MD5 { get; set; }
        public string CRC { get; set; }
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
