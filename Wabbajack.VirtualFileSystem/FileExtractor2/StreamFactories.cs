using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{

    public class UnmanagedStreamFactory : IStreamFactory
    {
        private readonly unsafe byte* _data;
        private readonly long _size;

        public unsafe UnmanagedStreamFactory(byte* data, long size)
        {
            _data = data;
            _size = size;
        }
        public async ValueTask<Stream> GetStream()
        {
            unsafe
            {
                return new UnmanagedMemoryStream(_data, _size);
            }
        }

        public DateTime LastModifiedUtc => DateTime.UtcNow;
        public IPath Name => (RelativePath)"Unmanaged Memory Stream";
    }


}
