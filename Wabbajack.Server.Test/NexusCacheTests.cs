using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class NexusCacheTests : ABuildServerSystemTest
    {
        public NexusCacheTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task TestCanGetModInfo()
        {
            var sqlService = Fixture.GetService<SqlService>();
            var modId = long.MaxValue >> 1;
            await sqlService.AddNexusModInfo(Game.SkyrimSpecialEdition, modId, DateTime.Now,
                new ModInfo {author = "Buzz", uploaded_by = "bille"});
            
            var api = await NexusApiClient.Get();
           
            var modInfoResponse = await api.GetModInfo(Game.SkyrimSpecialEdition, modId);

            Assert.Equal("Buzz", modInfoResponse.author);
            Assert.Equal("bille", modInfoResponse.uploaded_by);
            
        }
        
        [Fact]
        public async Task TestCanGetModFiles()
        {
            var sqlService = Fixture.GetService<SqlService>();
            var modId = long.MaxValue >> 1;
            await sqlService.AddNexusModFiles(Game.SkyrimSpecialEdition, modId,  DateTime.Now, 
                new NexusApiClient.GetModFilesResponse {files = new List<NexusFileInfo>
                {
                    new NexusFileInfo
                    {
                        file_name = "blerg"
                    }
                }});
           
            var api = await NexusApiClient.Get();
           
            var modInfoResponse = await api.GetModFiles(Game.SkyrimSpecialEdition, modId);

            Assert.Single(modInfoResponse.files);
            Assert.Equal("blerg", modInfoResponse.files.First().file_name);
        }

        [Fact]
        public async Task CanQueryAndFindNexusModfilesSlow()
        {
            var startTime = DateTime.UtcNow;
            var sql = Fixture.GetService<SqlService>();
            var validator = Fixture.GetService<ListValidator>();
            await sql.DeleteNexusModFilesUpdatedBeforeDate(Game.SkyrimSpecialEdition, 1137, DateTime.UtcNow);
            await sql.DeleteNexusModInfosUpdatedBeforeDate(Game.SkyrimSpecialEdition, 1137, DateTime.UtcNow);

            var result = await validator.SlowNexusModStats(new ValidationData(),
                new NexusDownloader.State {Game = Game.SkyrimSpecialEdition, ModID = 1137, FileID = 121449});
            Assert.Equal(ArchiveStatus.Valid, result);

            var gameId = Game.SkyrimSpecialEdition.MetaData().NexusGameId;
            var hs = await sql.AllNexusFiles();
           
            var found = hs.FirstOrDefault(h =>
                h.NexusGameId == gameId && h.ModId == 1137 && h.FileId == 121449);
            Assert.True(found != default);
            
            Assert.True(found.LastChecked > startTime && found.LastChecked < DateTime.UtcNow);

            // Delete with exactly the same date, shouldn't clear out the record
            await sql.DeleteNexusModFilesUpdatedBeforeDate(Game.SkyrimSpecialEdition, 1137, found.LastChecked);
            var hs2 = await sql.AllNexusFiles();
           
            var found2 = hs2.FirstOrDefault(h =>
                h.NexusGameId == gameId && h.ModId == 1137 && h.FileId == 121449);
            Assert.True(found != default);
            
            Assert.True(found2.LastChecked == found.LastChecked);
            
            // Delete all the records, it should now be gone
            await sql.DeleteNexusModFilesUpdatedBeforeDate(Game.SkyrimSpecialEdition, 1137, DateTime.UtcNow);
            var hs3 = await sql.AllNexusFiles();
            Assert.DoesNotContain(hs3, f => f.NexusGameId == gameId && f.ModId == 1137);

        }
    }
}
