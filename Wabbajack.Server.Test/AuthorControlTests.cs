using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Server.Test
{
    public class AuthorControlTests : ABuildServerSystemTest
    {
        public AuthorControlTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
            
        }

        [Fact]
        public async Task LoginRedirects()
        {
            var client = new HttpClient();
            var result =
                await client.GetStringAsync($"{Consts.WabbajackBuildServerUri}author_controls/login/{Fixture.APIKey}");

            Assert.Contains("Wabbajack Files", result);
        }
    }
}
