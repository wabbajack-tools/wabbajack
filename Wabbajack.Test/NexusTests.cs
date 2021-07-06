using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public class NexusTests : ATestBase
    {
        // Disable for now
        /*
        [Fact]
        public async Task CanGetNexusRSSUpdates()
        {
            var results = (await NexusUpdatesFeeds.GetUpdates()).ToArray();
            
            Utils.Log($"Loaded {results.Length} updates from the Nexus");

            foreach (var result in results)
            {
                Assert.True(DateTime.UtcNow - result.TimeStamp < TimeSpan.FromDays(1));
            }
        }*/

        public NexusTests(ITestOutputHelper output) : base(output)
        {
        }
    }
}
