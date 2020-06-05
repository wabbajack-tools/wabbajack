using System;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.BuildServer;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DataLayer;
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
            var settings = Fixture.GetService<AppSettings>();
            settings.ValidateModUpgrades = false;
            var validator = Fixture.GetService<ListValidator>();
            var nonNexus = Fixture.GetService<NonNexusDownloadValidator>();
            var modLists = await MakeModList("CanIndexAndUpdateFiles.txt");
            Consts.ModlistMetadataURL = modLists.ToString();
            
            
            var listDownloader = Fixture.GetService<ModListDownloader>();
            var downloader = Fixture.GetService<ArchiveDownloader>();
            var archiver = Fixture.GetService<ArchiveMaintainer>();
            var patcher = Fixture.GetService<PatchBuilder>();
            
            var sql = Fixture.GetService<SqlService>();
            var oldFileData = Encoding.UTF8.GetBytes("Cheese for Everyone!");
            var newFileData = Encoding.UTF8.GetBytes("Forks for Everyone!");
            var oldDataHash = oldFileData.xxHash();
            var newDataHash = newFileData.xxHash();
            
            var oldArchive = new Archive(new NexusDownloader.State {Game = Game.Enderal, ModID = 42, FileID = 10})
            {
                Size = oldFileData.Length,
                Hash = oldDataHash
            };
            var newArchive =  new Archive(new NexusDownloader.State {Game = Game.Enderal, ModID = 42, FileID = 11})
            {
                Size = newFileData.Length,
                Hash = newDataHash
            };

            await IngestData(archiver, oldFileData);
            await IngestData(archiver, newFileData);

            await sql.EnqueueDownload(oldArchive);
            var oldDownload = await sql.GetNextPendingDownload();
            await oldDownload.Finish(sql);

            await sql.EnqueueDownload(newArchive);
            var newDownload = await sql.GetNextPendingDownload();
            await newDownload.Finish(sql);
            

            await Assert.ThrowsAsync<HttpException>(async () => await ClientAPI.GetModUpgrade(oldArchive, newArchive, TimeSpan.Zero, TimeSpan.Zero));
            Assert.True(await patcher.Execute() > 1);

            Assert.Equal(new Uri("https://wabbajacktest.b-cdn.net/archive_updates/79223277e28e1b7b_3286c571d95f5666"),await ClientAPI.GetModUpgrade(oldArchive, newArchive, TimeSpan.Zero, TimeSpan.Zero));
        }

        [Fact]
        public async Task TestEndToEndArchiveUpdating()
        {
            var settings = Fixture.GetService<AppSettings>();
            settings.ValidateModUpgrades = false;

            var modLists = await MakeModList("TestEndToEndArchiveUpdating.txt");
            Consts.ModlistMetadataURL = modLists.ToString();
            
            
            var downloader = Fixture.GetService<ArchiveDownloader>();
            var archiver = Fixture.GetService<ArchiveMaintainer>();
            var patcher = Fixture.GetService<PatchBuilder>();
            
            var sql = Fixture.GetService<SqlService>();
            var oldFileData = Encoding.UTF8.GetBytes("Cheese for Everyone!");
            var newFileData = Encoding.UTF8.GetBytes("Forks for Everyone!");
            var oldDataHash = oldFileData.xxHash();
            var newDataHash = newFileData.xxHash();
            
            await "TestEndToEndArchiveUpdating.txt".RelativeTo(Fixture.ServerPublicFolder).WriteAllBytesAsync(oldFileData);
            
            var oldArchive = new Archive(new HTTPDownloader.State(MakeURL("TestEndToEndArchiveUpdating.txt")))
            {
                Size = oldFileData.Length,
                Hash = oldDataHash
            };

            await IngestData(archiver, oldFileData);
            await sql.EnqueueDownload(oldArchive);
            var oldDownload = await sql.GetNextPendingDownload();
            await oldDownload.Finish(sql);
            
            
            // Now update the file
            await"TestEndToEndArchiveUpdating.txt".RelativeTo(Fixture.ServerPublicFolder).WriteAllBytesAsync(newFileData);


            await using var tempFile = new TempFile();
            var pendingRequest = DownloadDispatcher.DownloadWithPossibleUpgrade(oldArchive, tempFile.Path);

            for (var times = 0; await downloader.Execute() == 0 && times < 40; times ++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }

            
            for (var times = 0; await patcher.Execute() == 0 && times < 40; times ++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }

            Assert.True(await pendingRequest);
            Assert.Equal(oldDataHash, await tempFile.Path.FileHashAsync());
        }

        private async Task IngestData(ArchiveMaintainer am, byte[] data)
        {
            await using var f = new TempFile();
            await f.Path.WriteAllBytesAsync(data);
            await am.Ingest(f.Path);
        }
    }
}
