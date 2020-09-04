using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public interface IStreamFactory
    {
        Task<Stream> GetStream();
        
        DateTime LastModifiedUtc { get; }
        
    }

    public class UnmanagedStreamFactory : IStreamFactory
    {
        private readonly unsafe byte* _data;
        private readonly long _size;

        public unsafe UnmanagedStreamFactory(byte* data, long size)
        {
            _data = data;
            _size = size;
        }
        public async Task<Stream> GetStream()
        {
            unsafe
            {
                return new UnmanagedMemoryStream(_data, _size);
            }
        }

        public DateTime LastModifiedUtc => DateTime.UtcNow;
    }

    public class NativeFileStreamFactory : IStreamFactory
    {
        private AbsolutePath _file;

        public NativeFileStreamFactory(AbsolutePath file)
        {
            _file = file;
        }
        public async Task<Stream> GetStream()
        {
            return await _file.OpenRead();
        }

        public DateTime LastModifiedUtc => _file.LastModifiedUtc;
    }
}
