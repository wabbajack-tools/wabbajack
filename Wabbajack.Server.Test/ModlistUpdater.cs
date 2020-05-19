using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.BuildServer;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Server.Test
{
    public class ModlistUpdater : ABuildServerSystemTest
    {
        public ModlistUpdater(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output,
            fixture)
        {
        }

        [Fact]
        public async Task CanIndexAndUpdateFiles()
        {
            var validator = Fixture.GetService<ListValidator>();
            var nonNexus = Fixture.GetService<NonNexusDownloadValidator>();
            var modLists = await MakeModList();
            Consts.ModlistMetadataURL = modLists.ToString();
            
            
            var listDownloader = Fixture.GetService<ModListDownloader>();
            var downloader = Fixture.GetService<ArchiveDownloader>();
            var archiver = Fixture.GetService<ArchiveMaintainer>();
            
            var sql = Fixture.GetService<SqlService>();
            var modId = long.MaxValue >> 1;
            var oldFileId = long.MaxValue >> 2;
            var newFileId = (long.MaxValue >> 2) + 1;

            var oldFileData = Encoding.UTF8.GetBytes("Cheese for Everyone!");
            var newFileData = Encoding.UTF8.GetBytes("Forks for Everyone!");
            var oldDataHash = oldFileData.xxHash();
            var newDataHash = newFileData.xxHash();

            Assert.Equal(2, await listDownloader.CheckForNewLists());
            Assert.Equal(1, await downloader.Execute());
            Assert.Equal(0, await nonNexus.Execute());
            Assert.Equal(0, await validator.Execute());
            
            
            Assert.True(archiver.HaveArchive(oldDataHash));
            Assert.False(archiver.HaveArchive(newDataHash));

            var status = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.Equal(0, status.ValidationSummary.Failed);
            

            // Update the archive
            await "test_archive.txt".RelativeTo(Fixture.ServerPublicFolder).WriteAllBytesAsync(newFileData);
            
            // Nothing new to do
            Assert.Equal(0, await listDownloader.CheckForNewLists());
            Assert.Equal(0, await downloader.Execute());
            
            // List now fails after we check the manual link
            Assert.Equal(1, await nonNexus.Execute());
            Assert.Equal(1, await validator.Execute());

            /*
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
*/
        }
    }
}
