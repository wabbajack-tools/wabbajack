using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Ceras;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using File = System.IO.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using SearchOption = System.IO.SearchOption;

namespace Wabbajack.VirtualFileSystem
{
    public class VirtualFile
    {
        public string Name { get; internal set; }

        [Exclude] private string _fullPath;

        [Exclude]
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

        [Exclude] public VirtualFile Parent { get; internal set; }

        [Exclude] public Context Context { get; set; }


        public ImmutableList<VirtualFile> Children { get; internal set; } = ImmutableList<VirtualFile>.Empty;

        [Exclude] public bool IsArchive => Children != null && Children.Count > 0;

        [Exclude] public bool IsNative => Parent == null;

        [Exclude]
        public IEnumerable<VirtualFile> ThisAndAllChildren =>
            Children.SelectMany(child => child.ThisAndAllChildren).Append(this);

        public static async Task<VirtualFile> Analyze(Context context, VirtualFile parent, string abs_path,
            string rel_path)
        {

            var hasher = abs_path.FileHashAsync();
            var fi = new FileInfo(abs_path);
            var self = new VirtualFile
            {
                Context = context,
                Name = rel_path,
                Parent = parent,
                Size = fi.Length,
                LastModified = fi.LastWriteTimeUtc.Ticks,
                LastAnalyzed = DateTime.Now.Ticks
            };

            if (FileExtractor.CanExtract(Path.GetExtension(abs_path)))
            {
                using (var tempFolder = context.GetTemporaryFolder())
                {
                    await FileExtractor.ExtractAll(abs_path, tempFolder.FullName);

                    var results = Channel.Create<VirtualFile>(1024);
                    var files = Directory.EnumerateFiles(tempFolder.FullName, "*", SearchOption.AllDirectories)
                        .ToChannel()
                        .UnorderedPipeline(results,
                            async abs_src =>
                            {
                                return await Analyze(context, self, abs_src, abs_src.RelativeTo(tempFolder.FullName));
                            });
                    self.Children = (await results.TakeAll()).ToImmutableList();
                }
            }

            self.Hash = await hasher;
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
                return Read(context,null, br);
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
            for (var idx = 0; idx < childrenCount; idx += 1)
            {
                vf.Children = vf.Children.Add(Read(context, vf, br));
            }

            return vf;
        }
    }
}
