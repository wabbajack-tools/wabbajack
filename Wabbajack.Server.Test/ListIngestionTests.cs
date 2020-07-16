using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Wabbajack.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Server.Test
{
    public class ListIngestionTests : ABuildServerSystemTest
    {
        public ListIngestionTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanIngestModLists()
        {
            await ClientAPI.SendModListDefinition(new ModList {Name = "sup"});
            await Task.Delay(500);
            
            Assert.Contains(AbsolutePath.EntryPoint.Combine("mod_list_definitions")
                .EnumerateFiles(false),
                f => DateTime.UtcNow - f.LastModifiedUtc < TimeSpan.FromSeconds(15));

            var data = AbsolutePath.EntryPoint.Combine("mod_list_definitions").EnumerateFiles(false)
                .OrderByDescending(f => f.LastModifiedUtc).First().FromJson<ModList>();

            Assert.Equal("sup", data.Name);
        }
    }
}
