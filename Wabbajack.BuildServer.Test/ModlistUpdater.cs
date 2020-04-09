using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.VirtualFileSystem;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace Wabbajack.BuildServer.Test
{
    public class ModlistUpdater : ABuildServerSystemTest
    {
        public ModlistUpdater(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact, Priority(0)]
        public async Task CanIndexFiles()
        {
            var sql = Fixture.GetService<SqlService>();
            var modId = long.MaxValue >> 1;
            var oldFileId = long.MaxValue >> 2;
            var newFileId = (long.MaxValue >> 2) + 1;

            var oldFileData = RandomData();
            var newFileData = RandomData();
            var oldDataHash = oldFileData.xxHash();
            var newDataHash = newFileData.xxHash();

            await "old_file_data.random".RelativeTo(Fixture.ServerPublicFolder).WriteAllBytesAsync(oldFileData);
            await "new_file_data.random".RelativeTo(Fixture.ServerPublicFolder).WriteAllBytesAsync(newFileData);

            await sql.EnqueueJob(new Job
            {
                Payload = new IndexJob
                {
                    Archive = new Archive
                    {
                        Name = "Oldfile",
                        State = new HTTPDownloader.State
                        {
                            Url = MakeURL("old_file_data.random"),
                        }
                    }
                }
            });
            
            await sql.EnqueueJob(new Job
            {
                Payload = new IndexJob
                {
                    Archive = new Archive
                    {
                        Name = "Newfile",
                        State = new HTTPDownloader.State
                        {
                            Url = MakeURL("new_file_data.random"),
                        }
                    }
                }
            });

            await RunAllJobs();
            
            Assert.True(await sql.HaveIndexdFile(oldDataHash));
            Assert.True(await sql.HaveIndexdFile(newDataHash));

            var settings = Fixture.GetService<AppSettings>();
            Assert.Equal($"Oldfile_{oldDataHash.ToHex()}_".RelativeTo(Fixture.ServerArchivesFolder), settings.PathForArchive(oldDataHash));
            Assert.Equal($"Newfile_{newDataHash.ToHex()}_".RelativeTo(Fixture.ServerArchivesFolder), settings.PathForArchive(newDataHash));


        }
        
    }
}
