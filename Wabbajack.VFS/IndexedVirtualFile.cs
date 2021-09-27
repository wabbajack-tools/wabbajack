using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Wabbajack.DTOs.Texture;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.VFS
{
    public class IndexedVirtualFile
    {
        public IPath Name { get; set; }
        public Hash Hash { get; set; }

        public ImageState? ImageState { get; set; }
        public long Size { get; set; }
        public List<IndexedVirtualFile> Children { get; set; } = new();

        private void Write(BinaryWriter bw)
        {
            bw.Write(Name.ToString()!);
            bw.Write((ulong)Hash);

            if (ImageState == null)
            {
                bw.Write(false);
            }
            else
            {
                bw.Write(true);
                WriteImageState(bw, ImageState);
            }

            bw.Write(Size);
            bw.Write(Children.Count);
            foreach (var file in Children)
                file.Write(bw);
        }

        private void WriteImageState(BinaryWriter bw, ImageState state)
        {
            bw.Write((ushort)state.Width);
            bw.Write((ushort)state.Height);
            bw.Write((byte)state.Format);
            state.PerceptualHash.Write(bw);
        }

        public static ImageState ReadImageState(BinaryReader br)
        {
            return new ImageState
            {
                Width = br.ReadUInt16(),
                Height = br.ReadUInt16(),
                Format = (DXGI_FORMAT)br.ReadByte(),
                PerceptualHash = PHash.Read(br)
            };
        }


        public void Write(Stream s)
        {
            using var cs = new GZipStream(s, CompressionLevel.Optimal, true);
            using var bw = new BinaryWriter(cs, Encoding.UTF8, true);
            Write(bw);
        }

        private static IndexedVirtualFile Read(BinaryReader br)
        {
            var ivf = new IndexedVirtualFile
            {
                Name = (RelativePath)br.ReadString(),
                Hash = Hash.FromULong(br.ReadUInt64())
            };

            if (br.ReadBoolean())
                ivf.ImageState = ReadImageState(br);

            ivf.Size = br.ReadInt64();

            var lst = new List<IndexedVirtualFile>();
            ivf.Children = lst;
            var count = br.ReadInt32();
            for (var x = 0; x < count; x++) lst.Add(Read(br));

            return ivf;
        }

        public static IndexedVirtualFile Read(Stream s)
        {
            using var cs = new GZipStream(s, CompressionMode.Decompress, true);
            using var br = new BinaryReader(cs);
            return Read(br);
        }
    }
}