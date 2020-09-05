using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Common
{
    public interface IStreamFactory
    {
        ValueTask<Stream> GetStream();
        
        DateTime LastModifiedUtc { get; }
        
        IPath Name { get; }
        
    }
    public class NativeFileStreamFactory : IStreamFactory
    {
        private AbsolutePath _file;

        public NativeFileStreamFactory(AbsolutePath file)
        {
            _file = file;
        }
        public async ValueTask<Stream> GetStream()
        {
            return await _file.OpenRead();
        }

        public DateTime LastModifiedUtc => _file.LastModifiedUtc;
        public IPath Name => _file;
    }
    
}
