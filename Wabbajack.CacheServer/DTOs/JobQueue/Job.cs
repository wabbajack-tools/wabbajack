using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Wabbajack.CacheServer.DTOs.JobQueue
{ 
    public class Job
    {
        public enum JobPriority : int
        {
            Low,
            Normal,
            High,
        }

        [BsonId]
        public Guid Id { get; set; }
        public DateTime? Started { get; set; }
        public DateTime? Ended { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public JobPriority Priority { get; set; } = JobPriority.Normal;
        public bool RequiresNexus { get; set; } = true;
        public AJobPayload Payload { get; set; }

        public static async Task<Guid> Enqueue(Job job)
        {
            await Server.Config.JobQueue.Connect().InsertOneAsync(job);
            return job.Id;
        }

        public static async Task<Job> GetNext()
        {
            var filter = new BsonDocument
            {
                {"query", new BsonDocument {{"Started", null}}},
                {"sort", new BsonDocument{{"Priority", -1}, {"Created", 1}}},

            };
            var update = new BsonDocument
            {
                {"update", new BsonDocument {{"$set", new BsonDocument {{"Started", DateTime.Now}}}}}
            };
            var job = await Server.Config.JobQueue.Connect().FindOneAndUpdateAsync<Job>(filter, update);
            return job;
        }

        public static async Task<Job> Finish(Job job)
        {
            var filter = new BsonDocument
            {
                {"query", new BsonDocument {{"Id", job.Id}}},
            };
            var update = new BsonDocument
            {
                {"update", new BsonDocument {{"$set", new BsonDocument {{"Ended", DateTime.Now}}}}}
            };
            var result = await Server.Config.JobQueue.Connect().FindOneAndUpdateAsync<Job>(filter, update);
            return result;
        }
    }
}
