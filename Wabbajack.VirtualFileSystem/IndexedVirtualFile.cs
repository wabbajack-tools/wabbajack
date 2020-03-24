using System.Collections.Generic;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    /// <summary>
    /// Response from the Build server for a indexed file
    /// </summary>
    public class IndexedVirtualFile
    {
        public IPath Name { get; set; }
        public Hash Hash { get; set; }
        public long Size { get; set; }
        public List<IndexedVirtualFile> Children { get; set; } = new List<IndexedVirtualFile>();
    }
}
