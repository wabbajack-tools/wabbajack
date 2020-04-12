using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    [Collection("ServerTests")]
   
    public class BasicTest : ABuildServerSystemTest
    {
        [Fact]
        public async Task CanEneuqueAndGetJobs()
        {
            var job = new Job {Payload = new GetNexusUpdatesJob()};
            var sqlService = Fixture.GetService<SqlService>();
            await sqlService.EnqueueJob(job);
            var found = await sqlService.GetJob();
            Assert.NotNull(found);
            Assert.IsAssignableFrom<GetNexusUpdatesJob>(found.Payload);
            found.Result = JobResult.Success();
            await sqlService.FinishJob(found);
        }
        
        [Fact]
        public async Task PriorityMatters()
        {
            await ClearJobQueue();
            var sqlService = Fixture.GetService<SqlService>();
            var priority = new List<Job.JobPriority>
            {
                Job.JobPriority.Normal, Job.JobPriority.High, Job.JobPriority.Low
            };
            foreach (var pri in priority) 
                await sqlService.EnqueueJob(new Job {Payload = new GetNexusUpdatesJob(), Priority = pri});

            foreach (var pri in priority.OrderByDescending(p => (int)p))
            {
                var found = await sqlService.GetJob();
                Assert.NotNull(found);
                Assert.Equal(pri, found.Priority);
                found.Result = JobResult.Success();
                
                // Finish the job so the next can run
                await sqlService.FinishJob(found);
            }
        }


        public BasicTest(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }
    }
}
