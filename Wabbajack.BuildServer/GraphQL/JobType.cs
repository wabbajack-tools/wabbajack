using GraphQL.Types;
using Wabbajack.BuildServer.Models.JobQueue;

namespace Wabbajack.BuildServer.GraphQL
{
    public class JobType : ObjectGraphType<Job>
    {
        public JobType()
        {
            Name = "Job";
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Unique Id of the Job");
            Field(x => x.Payload.Description).Description("Description of the job's behavior");
            Field(x => x.Created, type: typeof(DateTimeGraphType)).Description("Creation time of the Job");
            Field(x => x.Started, type: typeof(DateTimeGraphType)).Description("Started time of the Job");
            Field(x => x.Ended, type: typeof(DateTimeGraphType)).Description("Ended time of the Job");
        }
    }
}
