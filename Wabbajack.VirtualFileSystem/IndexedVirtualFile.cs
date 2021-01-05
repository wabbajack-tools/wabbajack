using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.VirtualFileSystem
{
    /// <summary>
    /// Response from the Build server for a indexed file
    /// </summary>
    [JsonName("IndexedVirtualFile")]
    public class IndexedVirtualFile
    {
        public IPath Name { get; set; }
        public Hash Hash { get; set; }
        public long Size { get; set; }
        public List<IndexedVirtualFile> Children { get; set; } = new List<IndexedVirtualFile>();

        private void Write(BinaryWriter bw)
        {
            bw.Write(Name.ToString());
            bw.Write((ulong)Hash);
            bw.Write(Size);
            bw.Write(Children.Count);
            foreach (var file in Children)
                file.Write(bw);
        }

        public void Write(Stream s)
        {
            using var bw = new BinaryWriter(s, Encoding.UTF8, true);
            bw.Write(Size);
            bw.Write(Children.Count);
            foreach (var file in Children)
                file.Write(bw);
        }

        private static IndexedVirtualFile Read(BinaryReader br)
        {
            var ivf = new IndexedVirtualFile
            {
                Name = (RelativePath)br.ReadString(),
                Hash = Hash.FromULong(br.ReadUInt64()),
                Size = br.ReadInt64(),
            };
            var lst = new List<IndexedVirtualFile>();
            ivf.Children = lst;
            var count = br.ReadInt32();
            for (int x = 0; x < count; x++)
            {
                lst.Add(Read(br));
            }

            return ivf;
        }

        public static IndexedVirtualFile Read(Stream s)
        {
            using var br = new BinaryReader(s);
            var ivf = new IndexedVirtualFile
            {
                Size = br.ReadInt64(),
            };
            var lst = new List<IndexedVirtualFile>();
            ivf.Children = lst;
            var count = br.ReadInt32();
            for (int x = 0; x < count; x++)
            {
                lst.Add(Read(br));
            }
            return ivf;
        }
    }
}
