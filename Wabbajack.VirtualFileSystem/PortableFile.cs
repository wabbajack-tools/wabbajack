using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.VirtualFileSystem
{
    public class PortableFile
    {
        public string Name { get; set; }
        public string Hash { get; set; }
        public string ParentHash { get; set; }
        public long Size { get; set; }
    }
}
