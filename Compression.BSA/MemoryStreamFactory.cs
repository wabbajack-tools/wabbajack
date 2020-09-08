using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Compression.BSA
{
    public class MemoryStreamFactory : IStreamFactory
    {
        private readonly MemoryStream _data;

        public MemoryStreamFactory(MemoryStream data)
        {
            _data = data;
        }
        public async ValueTask<Stream> GetStream()
        {
            return new MemoryStream(_data.GetBuffer(), 0, (int)_data.Length);
        }

        public DateTime LastModifiedUtc => DateTime.UtcNow;
        public IPath Name => (RelativePath)"BSA Memory Stream";
    }
    
    public class MemoryBufferFactory : IStreamFactory
    {
        private readonly byte[] _data;
        private int _size;

        public MemoryBufferFactory(byte[] data, int size)
        {
            _data = data;
            _size = size;
        }
        public async ValueTask<Stream> GetStream()
        {
            return new MemoryStream(_data, 0, _size);
        }

        public DateTime LastModifiedUtc => DateTime.UtcNow;
        public IPath Name => (RelativePath)"BSA Memory Stream";
    }
}
