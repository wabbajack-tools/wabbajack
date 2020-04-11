using System;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Controllers;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    [Collection("ServerTests")]
    public class BasicServerTests : ABuildServerSystemTest
    {

        
        
        [Fact]
        public async Task CanGetHeartbeat()
        {
            var heartbeat = (await _client.GetStringAsync(MakeURL("heartbeat"))).FromJsonString<Heartbeat.HeartbeatResult>();
            Assert.True(heartbeat.Uptime > TimeSpan.Zero);
        }

        [Fact]
        public async Task CanContactAuthedEndpoint()
        {
            var logs = await _authedClient.GetStringAsync(MakeURL("heartbeat/logs"));
            Assert.NotEmpty(logs);
        }

        public BasicServerTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }
    }
}
