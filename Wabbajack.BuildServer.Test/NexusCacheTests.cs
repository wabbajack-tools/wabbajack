using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.NexusApi;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace Wabbajack.BuildServer.Test
{
    public class NexusCacheTests : ABuildServerSystemTest
    {
        public NexusCacheTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact, Priority(2)]
        public async Task CanIngestNexusCacheExports()
        {
            await @"sql\nexus_export.json".RelativeTo(AbsolutePath.EntryPoint).CopyToAsync("nexus_export.json".RelativeTo(Fixture.ServerTempFolder));
            var result = await _authedClient.GetStringAsync(MakeURL("nexus_cache/ingest"));
            
            Assert.Equal("15024", result);
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
            var fileId = long.MaxValue >> 2;
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
