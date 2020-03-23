using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem
{
    public class PortableFile
    {
        public AbstractPath Name { get; set; }
        public Hash Hash { get; set; }
        public Hash ParentHash { get; set; }
        public long Size { get; set; }
    }
}
