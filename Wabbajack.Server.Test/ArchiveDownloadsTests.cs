using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DataLayer;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class ArchiveDownloadsTests : ABuildServerSystemTest
    {
        public ArchiveDownloadsTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanEnqueueDequeueAndUpdateDownloads()
        {
            await ClearDownloaderQueue();
            var state = new HTTPDownloader.State("http://www.google.com");
            var archive = new Archive(state);
            
            var service = Fixture.GetService<SqlService>();
            var id = await service.EnqueueDownload(archive);

            var toRun = await service.GetNextPendingDownload();
            
            Assert.Equal(id, toRun.Id);
            
            await toRun.Finish(service);
            await service.UpdatePendingDownload(toRun);

            toRun = await service.GetNextPendingDownload();
            Assert.Null(toRun);

            var allStates = await service.GetAllArchiveDownloads();
            Assert.Contains(state.PrimaryKeyString, allStates.Select(s => s.PrimaryKeyString));

        }

        private async Task ClearDownloaderQueue()
        {
            var service = Fixture.GetService<SqlService>();
            while (true)
            {
                var job = await service.GetNextPendingDownload();
                if (job == null) break;

                await job.Fail(service, "Canceled");
            }
        }
    }
}
