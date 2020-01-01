using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.CacheServer.ServerConfig
{
    public class IndexerConfig
    {
        public string DownloadDir { get; set; }
        public string TempDir { get; set; }

        public string ArchiveDir { get; set; }
    }
}
