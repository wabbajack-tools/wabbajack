using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.CacheServer.DTOs
{
    public class IndexedFile
    {
        [BsonId]
        public string Hash { get; set; }
        public string SHA256 { get; set; }
        public string SHA1 { get; set; }
        public string MD5 { get; set; }
        public string CRC { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public bool IsArchive { get; set; }
        public List<string> Children { get; set; } = new List<string>();
    }
}
