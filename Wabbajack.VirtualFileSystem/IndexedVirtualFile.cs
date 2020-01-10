using System.Collections.Generic;

namespace Wabbajack.VirtualFileSystem
{
    /// <summary>
    /// Response from the Build server for a indexed file
    /// </summary>
    public class IndexedVirtualFile
    {
        public string Name { get; set; }
        public string Hash { get; set; }
        public long Size { get; set; }
        public List<IndexedVirtualFile> Children { get; set; } = new List<IndexedVirtualFile>();
    }
}
