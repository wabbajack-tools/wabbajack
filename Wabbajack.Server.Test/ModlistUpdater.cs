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
using Wabbajack.Lib.Exceptions;
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
            Assert.Equal(1, await patcher.Execute());

            Assert.Equal(new Uri("https://wabbajacktest.b-cdn.net/archive_upgrades/79223277e28e1b7b_3286c571d95f5666"),await ClientAPI.GetModUpgrade(oldArchive, newArchive, TimeSpan.Zero, TimeSpan.Zero));

        }

        private async Task IngestData(ArchiveMaintainer am, byte[] data)
        {
            using var f = new TempFile();
            await f.Path.WriteAllBytesAsync(data);
            await am.Ingest(f.Path);
        }
    }
}
