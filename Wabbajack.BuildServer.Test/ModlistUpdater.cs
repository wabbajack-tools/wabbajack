using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
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
        public async Task CanIndexAndUpdateFiles()
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
                    Archive = new Archive(new HTTPDownloader.State(MakeURL("old_file_data.random")))
                    {
                        Name = "Oldfile",
                    }
                }
            });
            
            await sql.EnqueueJob(new Job
            {
                Payload = new IndexJob
                {
                    Archive = new Archive(new HTTPDownloader.State(MakeURL("new_file_data.random")))
                    {
                        Name = "Newfile",
                    }
                }
            });

            await RunAllJobs();
            
            Assert.True(await sql.HaveIndexdFile(oldDataHash));
            Assert.True(await sql.HaveIndexdFile(newDataHash));

            var settings = Fixture.GetService<AppSettings>();
            Assert.Equal($"Oldfile_{oldDataHash.ToHex()}_".RelativeTo(Fixture.ServerArchivesFolder), settings.PathForArchive(oldDataHash));
            Assert.Equal($"Newfile_{newDataHash.ToHex()}_".RelativeTo(Fixture.ServerArchivesFolder), settings.PathForArchive(newDataHash));

            Utils.Log($"Download Updating {oldDataHash} -> {newDataHash}");
            await using var conn = await sql.Open();

            await conn.ExecuteAsync("DELETE FROM dbo.DownloadStates WHERE Hash in (@OldHash, @NewHash);",
                new {OldHash = (long)oldDataHash, NewHash = (long)newDataHash});

            await sql.AddDownloadState(oldDataHash, new NexusDownloader.State
            {
                Game = Game.Oblivion,
                ModID = modId,
                FileID = oldFileId
            });

            await sql.AddDownloadState(newDataHash, new NexusDownloader.State
            {
                Game = Game.Oblivion,
                ModID = modId,
                FileID = newFileId
            });
            
            Assert.NotNull(await sql.GetNexusStateByHash(oldDataHash));
            Assert.NotNull(await sql.GetNexusStateByHash(newDataHash));

            // No nexus info, so no upgrade
            var noUpgrade = await ClientAPI.GetModUpgrade(oldDataHash);
            Assert.Null(noUpgrade);

            // Add Nexus info
            await sql.AddNexusModFiles(Game.Oblivion, modId, DateTime.Now,
                new NexusApiClient.GetModFilesResponse
                {
                    files = new List<NexusFileInfo>
                    {
                        new NexusFileInfo {category_name = "MAIN", file_id = newFileId, file_name = "New File"},
                        new NexusFileInfo {category_name = null, file_id = oldFileId, file_name = "Old File"}
                    }
                });

            
            var enqueuedUpgrade = await ClientAPI.GetModUpgrade(oldDataHash);
            
            // Not Null because upgrade was enqueued
            Assert.NotNull(enqueuedUpgrade);

            await RunAllJobs();

            Assert.True($"{oldDataHash.ToHex()}_{newDataHash.ToHex()}".RelativeTo(Fixture.ServerUpdatesFolder).IsFile);

        }
    }
}
