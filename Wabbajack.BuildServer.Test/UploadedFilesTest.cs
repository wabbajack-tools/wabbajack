using System;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class UploadedFilesTest : ABuildServerSystemTest
    {

        public UploadedFilesTest(ITestOutputHelper helper, BuildServerFixture fixture) : base(helper, fixture)
        {
        }


        [Fact]
        public async Task CanIngestMongoDBExports()
        {
            @"sql\uploaded_files_ingest.json".RelativeTo(AbsolutePath.EntryPoint).CopyTo(Fixture.ServerTempFolder.Combine("uploaded_files_ingest.json"));
            using var response = await _authedClient.GetAsync(MakeURL("ingest/uploaded_files/uploaded_files_ingest.json"));
            var result = await response.Content.ReadAsStringAsync();
            Utils.Log("Loaded: " + result);

            
            Assert.Equal("4", result);
        }
    }
}
