using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.VirtualFileSystem
{
    public class VirtualFile
    {
        private string _fullPath;

        private string _stagedPath;
        public string Name { get; internal set; }

        public string FullPath
        {
            get
            {
                if (_fullPath != null) return _fullPath;
                var cur = this;
                var acc = new LinkedList<string>();
                while (cur != null)
                {
                    acc.AddFirst(cur.Name);
                    cur = cur.Parent;
                }

                _fullPath = string.Join("|", acc);

                return _fullPath;
            }
        }

        public string Hash { get; internal set; }
        public long Size { get; internal set; }

        public long LastModified { get; internal set; }

        public long LastAnalyzed { get; internal set; }

        public VirtualFile Parent { get; internal set; }

        public Context Context { get; set; }

        public string StagedPath
        {
            get
            {
                if (IsNative)
                    return Name;
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

        public static async Task<VirtualFile> Analyze(Context context, VirtualFile parent, string abs_path,
            string rel_path)
        {
            var fi = new FileInfo(abs_path);
            var self = new VirtualFile
            {
                Context = context,
                Name = rel_path,
                Parent = parent,
                Size = fi.Length,
                LastModified = fi.LastWriteTimeUtc.Ticks,
                LastAnalyzed = DateTime.Now.Ticks,
                Hash = abs_path.FileHash()
            };

            if (FileExtractor.CanExtract(abs_path))
            {

                using (var tempFolder = context.GetTemporaryFolder())
                {
                    await FileExtractor.ExtractAll(context.Queue, abs_path, tempFolder.FullName);

                    var list = await Directory.EnumerateFiles(tempFolder.FullName, "*", SearchOption.AllDirectories)
                                        .PMap(context.Queue, abs_src => Analyze(context, self, abs_src, abs_src.RelativeTo(tempFolder.FullName)));

                    self.Children = list.ToImmutableList();
                }

            }

            return self;
        }

        public void Write(MemoryStream ms)
        {
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                Write(bw);
            }
        }

        private void Write(BinaryWriter bw)
        {
            bw.Write(Name);
            bw.Write(Hash);
            bw.Write(Size);
            bw.Write(LastModified);
            bw.Write(LastAnalyzed);
            bw.Write(Children.Count);
            foreach (var child in Children)
                child.Write(bw);
        }

        public static VirtualFile Read(Context context, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                return Read(context, null, br);
            }
        }

        private static VirtualFile Read(Context context, VirtualFile parent, BinaryReader br)
        {
            var vf = new VirtualFile
            {
                Context = context,
                Parent = parent,
                Name = br.ReadString(),
                Hash = br.ReadString(),
                Size = br.ReadInt64(),
                LastModified = br.ReadInt64(),
                LastAnalyzed = br.ReadInt64(),
                Children = ImmutableList<VirtualFile>.Empty
            };

            var childrenCount = br.ReadInt32();
            for (var idx = 0; idx < childrenCount; idx += 1) vf.Children = vf.Children.Add(Read(context, vf, br));

            return vf;
        }

        public static VirtualFile CreateFromPortable(Context context,
            Dictionary<string, IEnumerable<PortableFile>> state, Dictionary<string, string> links,
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
            Dictionary<string, IEnumerable<PortableFile>> state, PortableFile portableFile)
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
            path[0] = FilesInFullPath.First().Hash;
            
            var idx = 1;

            foreach (var itm in FilesInFullPath.Skip(1))
            {
                path[idx] = itm.Name;
                idx += 1;
            }
            return path;
        }

        public Stream OpenRead()
        {
            return File.OpenRead(StagedPath);
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
        private readonly string _fullPath;

        public UnstagedFileException(string fullPath) : base($"File {fullPath} is unstaged, cannot get staged name")
        {
            _fullPath = fullPath;
        }
    }
}
