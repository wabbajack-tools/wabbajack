using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.ExtractedFiles
{
    public class ExtractedMemoryFile : IExtractedFile
    {
        private IStreamFactory _factory;

        public ExtractedMemoryFile(IStreamFactory factory)
        {
            _factory = factory;
        }


        public ValueTask<Stream> GetStream()
        {
            return _factory.GetStream();
        }

        public DateTime LastModifiedUtc => _factory.LastModifiedUtc;
        public IPath Name => _factory.Name;
        public async ValueTask Move(AbsolutePath newPath)
        {
            await using var stream = await _factory.GetStream();
            await newPath.WriteAllAsync(stream);
        }

        public bool CanMove { get; set; } = true;
    }
}
