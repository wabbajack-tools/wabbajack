using System;
using System.IO;
using System.Threading.Tasks;
using DirectXTexNet;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.ImageHashing
{
    [JsonName("ImageState")]
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

        public static async Task<ImageState?> FromImageStream(Stream stream, Extension ext, bool takeStreamOwnership = true)
        {
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            if (takeStreamOwnership) await stream.DisposeAsync();

            DDSImage? img = default;
            try
            {
                if (ext == new Extension(".dds"))
                    img = DDSImage.FromDDSMemory(ms.GetBuffer());
                else if (ext == new Extension(".tga"))
                {
                    img = DDSImage.FromTGAMemory(ms.GetBuffer());
                }
                else
                {
                    throw new NotImplementedException("Only DDS and TGA files supported by PHash");
                }

                return img.ImageState();

            }
            catch (Exception ex)
            {
                Utils.Log($"Error getting ImageState: {ex}");
                return null;
            }
            finally
            {
                img?.Dispose();
            }
        }
    }
}
