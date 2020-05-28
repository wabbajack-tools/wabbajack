using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class ArchiveMaintainerTests : ABuildServerSystemTest
    {
        public ArchiveMaintainerTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }
        
        [Fact]
        public async Task CanIngestFiles()
        {
            var maintainer = Fixture.GetService<ArchiveMaintainer>();
            await using var tf = new TempFile();
            await using var tf2 = new TempFile();
            
            await tf.Path.WriteAllBytesAsync(RandomData(1024));
            await tf.Path.CopyToAsync(tf2.Path);

            
            var hash = await tf.Path.FileHashAsync();
            await maintainer.Ingest(tf.Path);
            
            Assert.True(maintainer.TryGetPath(hash, out var found));
            Assert.Equal(await tf2.Path.ReadAllBytesAsync(), await found.ReadAllBytesAsync());
        }
        
        
        [Fact]
        public async Task IngestsExistingFiles()
        {
            var maintainer = Fixture.GetService<ArchiveMaintainer>();
            await using var tf = new TempFile();
            
            await tf.Path.WriteAllBytesAsync(RandomData(1024));
            var hash = await tf.Path.FileHashAsync();
            
            await tf.Path.CopyToAsync(Fixture.ServerArchivesFolder.Combine(hash.ToHex()));
            maintainer.Start();

            Assert.True(maintainer.TryGetPath(hash, out var found));
        }
    }
}
