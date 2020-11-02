using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
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
            var hash = Hash.FromBase64("eSIyd+KOG3s=");

            var archive =
                new Archive(new WabbajackCDNDownloader.State(new Uri(
                    "https://wabbajack.b-cdn.net/WABBAJACK_TEST_FILE.zip_a1a3e961-5c0b-4ccf-84b4-7aa437d9640d")))
                {
                    Size = 20, Hash = hash
                };

            await sql.EnqueueDownload(archive);
            await sql.UpsertMirroredFile(new MirroredFile()
            {
                Created = DateTime.UtcNow,
                Uploaded = DateTime.UtcNow,
                Hash = hash,
                Rationale = "Test File"
            });
            var dld = await sql.GetNextPendingDownload();
            await dld.Finish(sql);


            var state = await ClientAPI.InferDownloadState(archive.Hash);
            Assert.Equal(archive.State.GetMetaIniString(), state!.GetMetaIniString());

            var archives = await (await ClientAPI.GetClient()).GetJsonAsync<Archive[]>(
                $"{Consts.WabbajackBuildServerUri}mod_files/by_hash/{hash.ToHex()}");
            
            Assert.True(archives.Length >= 2);
            Assert.NotNull(archives.FirstOrDefault(a => a.State is WabbajackCDNDownloader.State));

            await sql.DeleteMirroredFile(hash);

        }
    }
}
