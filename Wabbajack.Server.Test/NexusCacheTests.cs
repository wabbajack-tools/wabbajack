using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
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
    }
}
