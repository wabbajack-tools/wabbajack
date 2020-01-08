using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.BuildServer.Models
{
    public class IndexedFile
    {
        [BsonId]
        public string Hash { get; set; }
        public string SHA256 { get; set; }
        public string SHA1 { get; set; }
        public string MD5 { get; set; }
        public string CRC { get; set; }
        public long Size { get; set; }
        public bool IsArchive { get; set; }
        public List<ChildFile> Children { get; set; } = new List<ChildFile>();
    }

    public class ChildFile
    {
        public string Name;
        public string Extension;
        public string Hash;
    }
}
