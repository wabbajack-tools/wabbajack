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
        protected AbsolutePath _file;

        public NativeFileStreamFactory(AbsolutePath file, IPath path)
        {
            _file = file;
            Name = path;
        }
        
        public NativeFileStreamFactory(AbsolutePath file)
        {
            _file = file;
            Name = file;
        }
        public async ValueTask<Stream> GetStream()
        {
            return await _file.OpenRead();
        }

        private DateTime? _lastModifiedCache = null;
        public DateTime LastModifiedUtc
        {
            get
            {
                _lastModifiedCache ??= _file.LastModifiedUtc;
                return _lastModifiedCache.Value;
            }
        }

        public IPath Name { get; }
    }
    
}
