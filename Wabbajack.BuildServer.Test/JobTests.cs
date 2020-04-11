using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    
    public class JobTests : ABuildServerSystemTest
    {
        public JobTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanRunNexusUpdateJob()
        {
            var sql = Fixture.GetService<SqlService>();

            var oldRecords = await NexusUpdatesFeeds.GetUpdates();
            foreach (var record in oldRecords)
            {
                await sql.AddNexusModInfo(record.Game, record.ModId, DateTime.UtcNow - TimeSpan.FromDays(1),
                    new ModInfo());
                await sql.AddNexusModFiles(record.Game, record.ModId, DateTime.UtcNow - TimeSpan.FromDays(1),
                    new NexusApiClient.GetModFilesResponse());
                
                Assert.NotNull(await sql.GetModFiles(record.Game, record.ModId));
                Assert.NotNull(await sql.GetNexusModInfoString(record.Game, record.ModId));
            }

            Utils.Log($"Ingested {oldRecords.Count()} nexus records");
            
            // We know this will load the same records as above, but the date will be more recent, so the above records
            // should no longer exist in SQL after this job is run
            await sql.EnqueueJob(new Job {Payload = new GetNexusUpdatesJob()});
            await RunAllJobs();

            foreach (var record in oldRecords)
            {
                Assert.Null(await sql.GetModFiles(record.Game, record.ModId));
                Assert.Null(await sql.GetNexusModInfoString(record.Game, record.ModId));
            }
        }

        [Fact]
        public async Task CanPrimeTheNexusCache()
        {
            var sql = Fixture.GetService<SqlService>();

            Assert.True(await GetNexusUpdatesJob.UpdateNexusCacheFast(sql) > 0);
            Assert.True(await GetNexusUpdatesJob.UpdateNexusCacheFast(sql) == 0);
        }
    }
}
