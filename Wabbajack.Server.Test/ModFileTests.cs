using System;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DataLayer;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Server.Test
{
    
    public class ModFileTests : ABuildServerSystemTest
    {
        public ModFileTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
            
        }

        [Fact]
        public async Task CanGetDownloadStates()
        {
            var sql = Fixture.GetService<SqlService>();

            var archive =
                new Archive(new WabbajackCDNDownloader.State(new Uri(
                    "https://wabbajack.b-cdn.net/WABBAJACK_TEST_FILE.zip_a1a3e961-5c0b-4ccf-84b4-7aa437d9640d")))
                {
                    Size = 20, Hash = Hash.FromBase64("eSIyd+KOG3s=")
                };

            await sql.EnqueueDownload(archive);
            var dld = await sql.GetNextPendingDownload();
            await dld.Finish(sql);


            var state = await ClientAPI.InferDownloadState(archive.Hash);
            
            Assert.Equal(archive.State.GetMetaIniString(), state!.GetMetaIniString());

        }
    }
}
