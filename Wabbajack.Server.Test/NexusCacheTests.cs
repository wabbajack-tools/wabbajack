using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.FileUploader;
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
        public async Task TestCanPurgeModInfo()
        {
            var sqlService = Fixture.GetService<SqlService>();
            var modId = long.MaxValue >> 3;
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

            await AuthorAPI.PurgeNexusModInfo(modId);
        }

        [Fact]
        public async Task CanQueryAndFindNexusModfilesFast()
        {
            var startTime = DateTime.UtcNow;
            var sql = Fixture.GetService<SqlService>();
            var validator = Fixture.GetService<ListValidator>();
            await sql.DeleteNexusModFilesUpdatedBeforeDate(Game.SkyrimSpecialEdition, 1137, DateTime.UtcNow);
            await sql.DeleteNexusModInfosUpdatedBeforeDate(Game.SkyrimSpecialEdition, 1137, DateTime.UtcNow);

            var result = await validator.FastNexusModStats(new NexusDownloader.State {Game = Game.SkyrimSpecialEdition, ModID = 1137, FileID = 121449});
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


        [JsonName("DateBox")]
        class Box
        {
            public DateTime Value
            {
                get;
                set;
            }
        }
        [Fact]
        public async Task DatesConvertProperly()
        {

            var a = DateTime.Now;
            var b = DateTime.UtcNow;
            
            Assert.NotEqual(a, new Box{Value = a}.ToJson().FromJsonString<Box>().Value);
            Assert.Equal(b, new Box{Value = b}.ToJson().FromJsonString<Box>().Value);
            Assert.NotEqual(a.Hour, b.Hour);
            Assert.Equal(b.Hour, new Box{Value = a}.ToJson().FromJsonString<Box>().Value.Hour);
            
            
            var ts = (long)1589528640;
            var ds = DateTime.Parse("2020-05-15 07:44:00.000");
            Assert.Equal(ds, ts.AsUnixTime());
            Assert.Equal(ts, (long)ds.AsUnixTime());
            Assert.Equal(ts, (long)ts.AsUnixTime().AsUnixTime());

        }

        [Fact]
        public async Task CanGetAndSetPermissions()
        {
            var game = Game.Oblivion;
            var modId = 4424;
            var sql = Fixture.GetService<SqlService>();

            foreach (HTMLInterface.PermissionValue result in Enum.GetValues(typeof(HTMLInterface.PermissionValue)))
            {
                await sql.SetNexusPermission(game, modId, result);
                Assert.Equal(result, (await sql.GetNexusPermissions())[(game, modId)]);
            }
        }
    }
}
