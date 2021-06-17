using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shipwreck.Phash;
using Wabbajack.Common;

namespace Wabbajack.ImageHashing
{
    [JsonConverter(typeof(PHashJsonConverter))]
    public struct PHash
    {
        private const int SIZE = 40;
        private readonly int _hash;

        public byte[] Data { get; }

        private PHash(byte[] data)
        {
            Data = data;
            if (Data.Length != SIZE)
                throw new DataException();
            
            long h = 0;
            h |= Data[0];
            h <<= 8;
            h |= Data[1];
            h <<= 8;
            h |= Data[2];
            h <<= 8;
            h |= Data[3];
            h <<= 8;
            _hash = (int)h;
        }

        public static PHash FromBase64(string base64)
        {
            var data = base64.FromBase64();
            if (data.Length != SIZE)
                throw new DataException();
            return new PHash(data);
        }
        
        public static PHash Read(BinaryReader br)
        {
            return new (br.ReadBytes(SIZE));
        }

        public void Write(BinaryWriter br)
        {
            if (_hash == 0)
                br.Write(new byte[SIZE]);
            else 
                br.Write(Data);
        }
        
        public static PHash FromDigest(Digest digest)
        {
            return new(digest.Coefficients);
        }

        public float Similarity(PHash other)
        {
            return ImagePhash.GetCrossCorrelation(this.Data, other.Data);
        }

        public override string ToString()
        {
            return Data.ToBase64();
        }

        public override int GetHashCode()
        {
            long h = 0;
            h |= Data[0];
            h <<= 8;
            h |= Data[1];
            h <<= 8;
            h |= Data[2];
            h <<= 8;
            h |= Data[3];
            h <<= 8;
            return (int)h;
        }

        public static async Task<PHash> FromStream(Stream stream, Extension ext, bool takeStreamOwnership = true)
        {
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            if (takeStreamOwnership) await stream.DisposeAsync();

            DDSImage img;
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
            return img.PerceptionHash();
        }

        public static async Task<PHash> FromFile(AbsolutePath path)
        {
            await using var s = await path.OpenRead();
            return await FromStream(s, path.Extension);

        }
    }
    
            
    public class PHashJsonConverter : JsonConverter<PHash>
    {
        public override void WriteJson(JsonWriter writer, PHash value, JsonSerializer serializer)
        {
            writer.WriteValue(value.Data.ToBase64());
        }

        public override PHash ReadJson(JsonReader reader, Type objectType, PHash existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            return PHash.FromBase64((string)reader.Value!);
        }
    }
}
