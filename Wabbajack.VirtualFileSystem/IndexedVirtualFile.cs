using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.VirtualFileSystem
{
    /// <summary>
    /// Response from the Build server for a indexed file
    /// </summary>
    [JsonName("IndexedVirtualFile")]
    public class IndexedVirtualFile
    {
        public IPath Name { get; set; }
        public Hash Hash { get; set; }
        public long Size { get; set; }
        public List<IndexedVirtualFile> Children { get; set; } = new List<IndexedVirtualFile>();
    }
}
