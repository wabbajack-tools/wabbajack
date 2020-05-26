using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public interface IExtractedFile
    {
        public Task<Hash> HashAsync();
        public DateTime LastModifiedUtc { get; }
        public long Size { get; }

        public ValueTask<Stream> OpenRead();

        public Task<bool> CanExtract();

        public Task<ExtractedFiles> ExtractAll(WorkQueue queue, IEnumerable<RelativePath> Only = null);

        public Task MoveTo(AbsolutePath path);

    }
}
