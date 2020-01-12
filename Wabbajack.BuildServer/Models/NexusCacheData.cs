using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Wabbajack.BuildServer.Models
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
