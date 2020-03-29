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
    public class BasicTest : ABuildServerTest
    {
        [Fact]
        public async Task CanEneuqueAndGetJobs()
        {
            var job = new Job {Payload = new GetNexusUpdatesJob()};
            await _sqlService.EnqueueJob(job);
            var found = await _sqlService.GetJob();
            Assert.NotNull(found);
            Assert.IsAssignableFrom<GetNexusUpdatesJob>(found.Payload);
        }
        
        [Fact]
        public async Task PriorityMatters()
        {
            var priority = new List<Job.JobPriority>
            {
                Job.JobPriority.Normal, Job.JobPriority.High, Job.JobPriority.Low
            };
            foreach (var pri in priority) 
                await _sqlService.EnqueueJob(new Job {Payload = new GetNexusUpdatesJob(), Priority = pri});

            foreach (var pri in priority.OrderByDescending(p => (int)p))
            {
                var found = await _sqlService.GetJob();
                Assert.NotNull(found);
                Assert.Equal(pri, found.Priority);
            }
        }

        public BasicTest(ITestOutputHelper helper) : base(helper)
        {
            
            
        }
    }
}
