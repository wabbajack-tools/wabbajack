using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Test;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Server.Test
{
    public class DiscordFrontentTests: ABuildServerSystemTest
    {
        public DiscordFrontentTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanLogIn()
        {
            var frontend = Fixture.GetService<DiscordFrontend>();
            frontend.Start();
        }
        
    }
}
