using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace Wabbajack.CacheServer.DTOs
{
    public class NexusCacheData<T>
    {
        [BsonId]
        public string Path { get; set; }
        public T Data { get; set; }
        public string Game { get; set; }
        public string ModId { get; set; }

        public DateTime LastCheckedUTC { get; set; } = DateTime.UtcNow;

        [BsonIgnoreIfNull]
        public string FileId { get; set; }

    }
}
