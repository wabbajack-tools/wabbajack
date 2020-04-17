using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Compression.BSA;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public class ExtractedBSAFile : IExtractedFile
    {
        private readonly IFile _file;
        public ExtractedBSAFile(IFile file)
        {
            _file = file;
        }

        public RelativePath Path => _file.Path;

        public async Task<Hash> HashAsync()
        {
            await using var stream = OpenRead();
            return stream.xxHash();
        }
        public DateTime LastModifiedUtc => DateTime.UtcNow;
        public long Size => _file.Size;
        public Stream OpenRead()
        {
            var ms = new MemoryStream();
            _file.CopyDataTo(ms);
            ms.Position = 0;
            return ms;
        }

        public async Task<bool> CanExtract()
        {
            return false;
        }

        public Task<ExtractedFiles> ExtractAll(WorkQueue queue, IEnumerable<RelativePath> OnlyFiles)
        {
            throw new Exception("BSAs can't contain archives");
        }

        public async Task MoveTo(AbsolutePath path)
        {
            await using var fs = path.Create();
            _file.CopyDataTo(fs);
        }
    }
}
