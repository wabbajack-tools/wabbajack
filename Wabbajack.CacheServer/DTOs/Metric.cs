using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace Wabbajack.CacheServer.DTOs
{
    public class Metric
    {
        [BsonId]
        public ObjectId Id;
        public DateTime Timestamp;
        public string Action;
        public string Subject;
        public string MetricsKey;
    }
}
