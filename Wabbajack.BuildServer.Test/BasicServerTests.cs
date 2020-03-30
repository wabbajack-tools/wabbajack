using System;
using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class BasicServerTests : ABuildServerSystemTest
    {
        public BasicServerTests(ITestOutputHelper output, BuildServerFixture fixture) : base(output, fixture)
        {
        }
        
        
        [Fact]
        public async Task CanGetHeartbeat()
        {
            var heartbeat = (await _client.GetStringAsync(MakeURL("heartbeat"))).FromJSONString<string>();
            Assert.True(TimeSpan.Parse(heartbeat) > TimeSpan.Zero);
        }

        [Fact]
        public async Task CanContactAuthedEndpoint()
        {
            var logs = await _authedClient.GetStringAsync(MakeURL("heartbeat/logs"));
            Assert.NotEmpty(logs);
        }

    }
}
