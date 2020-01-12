using GraphQL.Types;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;

namespace Wabbajack.BuildServer.GraphQL
{
    public class Mutation : ObjectGraphType
    {
        public Mutation(DBContext db)
        {
            FieldAsync<IdGraphType>("pollNexusForUpdates",
                resolve: async context =>
                {
                    var job = new Job {Payload = new GetNexusUpdatesJob()};
                    await db.Jobs.InsertOneAsync(job);
                    return job.Id;
                });
        }
        
    }
}
