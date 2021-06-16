using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Shipwreck.Phash;
using Wabbajack.Common;

namespace Wabbajack.ImageHashing
{
    public struct PHash
    {
        private const int SIZE = 40;
        private readonly byte[] _data;
        private readonly int _hash;

        private PHash(byte[] data)
        {
            _data = data;
            if (_data.Length != SIZE)
                throw new DataException();
            
            long h = 0;
            h |= _data[0];
            h <<= 8;
            h |= _data[1];
            h <<= 8;
            h |= _data[2];
            h <<= 8;
            h |= _data[3];
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
                br.Write(_data);
        }
        
        public static PHash FromDigest(Digest digest)
        {
            return new(digest.Coefficients);
        }

        public float Similarity(PHash other)
        {
            return ImagePhash.GetCrossCorrelation(this._data, other._data);
        }

        public override string ToString()
        {
            return _data.ToBase64();
        }

        public override int GetHashCode()
        {
            long h = 0;
            h |= _data[0];
            h <<= 8;
            h |= _data[1];
            h <<= 8;
            h |= _data[2];
            h <<= 8;
            h |= _data[3];
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
    }
}
