using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public class RootDiskFile : ExtractedDiskFile
    {
        public RootDiskFile(AbsolutePath path) : base(path)
        {
        }
        
        public override async Task<Hash> HashAsync()
        {
            return await _path.FileHashCachedAsync();
        }
    }
}
