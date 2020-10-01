using System.Threading.Tasks;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Wabbajack.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Server.Test
{
    public class VirusCheckTests : ABuildServerSystemTest
    {
        public VirusCheckTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CheckVirus()
        {
            var tmpFile = new TempFile();

            var meta = Game.SkyrimSpecialEdition.MetaData();
            var srcFile = meta.GameLocation().Combine(meta.MainExecutable!);

            await srcFile.CopyToAsync(tmpFile.Path);

            using (var s = await tmpFile.Path.OpenWrite())
            {
                s.Position = 1000;
                s.WriteByte(42);
            }
            
            Assert.True(await VirusScanner.ShouldScan(tmpFile.Path));
            
            Assert.Equal(VirusScanner.Result.NotMalware, await ClientAPI.GetVirusScanResult(tmpFile.Path));
        }
    }
}
