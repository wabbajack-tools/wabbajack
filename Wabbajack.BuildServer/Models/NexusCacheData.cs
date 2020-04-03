using System;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Wabbajack.BuildServer.Models
{
    public class NexusCacheData<T>
    {
        [BsonId]
        public string Path { get; set; }
        public T Data { get; set; }
        public string Game { get; set; }
        
        [BsonIgnoreIfNull]
        public long ModId { get; set; }

        public DateTime LastCheckedUTC { get; set; } = DateTime.UtcNow;

        [BsonIgnoreIfNull]
        public string FileId { get; set; }

        public async Task Upsert(IMongoCollection<NexusCacheData<T>> coll)
        {
            await coll.FindOneAndReplaceAsync<NexusCacheData<T>>(s => s.Path == Path, this, new FindOneAndReplaceOptions<NexusCacheData<T>> {IsUpsert = true});
        }
    }
}
