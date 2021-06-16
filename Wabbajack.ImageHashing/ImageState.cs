using System.IO;
using DirectXTexNet;
using Wabbajack.Common;

namespace Wabbajack.ImageHashing
{
    public class ImageState
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public DXGI_FORMAT Format { get; set; }
        public PHash PerceptualHash { get; set; }

        public static ImageState Read(BinaryReader br)
        {
            return new()
            {
                Width = br.ReadUInt16(),
                Height = br.ReadUInt16(),
                Format = (DXGI_FORMAT)br.ReadByte(),
                PerceptualHash = PHash.Read(br)
            };
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((ushort)Width);
            bw.Write((ushort)Height);
            bw.Write((byte)Format);
            PerceptualHash.Write(bw);
        }
    }
}
