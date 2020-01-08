using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Wabbajack.BuildServer.Models.JobQueue
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

        public JobResult Result { get; set; }
        public bool RequiresNexus { get; set; } = true;
        public AJobPayload Payload { get; set; }

        public static async Task<Guid> Enqueue(DBContext db, Job job)
        {
            await db.Jobs.InsertOneAsync(job);
            return job.Id;
        }

        public static async Task<Job> GetNext(DBContext db)
        {
            var filter = new BsonDocument
            {
                {"Started", BsonNull.Value}
            };
            var update = new BsonDocument
            {
                {"$set", new BsonDocument {{"Started", DateTime.Now}}}
            };
            var sort = new {Priority=-1, Created=1}.ToBsonDocument();
            var job = await db.Jobs.FindOneAndUpdateAsync<Job>(filter, update, new FindOneAndUpdateOptions<Job>{Sort = sort});
            return job;
        }

        public static async Task<Job> Finish(DBContext db, Job job, JobResult jobResult)
        {
            var filter = new BsonDocument
            {
                {"query", new BsonDocument {{"Id", job.Id}}},
            };
            var update = new BsonDocument
            {
                {"$set", new BsonDocument {{"Ended", DateTime.Now}, {"Result", jobResult.ToBsonDocument()}}}
            };
            var result = await db.Jobs.FindOneAndUpdateAsync<Job>(filter, update);
            return result;
        }
    }
}
