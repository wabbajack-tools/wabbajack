using System;
using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class LoginTests : ABuildServerSystemTest
    {
        public LoginTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanCreateLogins()
        {
            var newUserName = Guid.NewGuid().ToString();

            var newKey = await _authedClient.GetStringAsync(MakeURL($"users/add/{newUserName}"));
            
            Assert.NotEmpty(newKey);
            Assert.NotNull(newKey);
            Assert.NotEqual(newKey, Fixture.APIKey);


            var done = await _authedClient.GetStringAsync(MakeURL("users/export"));
            Assert.Equal("done", done);
            
            foreach (var (userName, apiKey) in new[] {(newUserName, newKey), (Fixture.User, Fixture.APIKey)})
            {
                var exported = await Fixture.ServerTempFolder.Combine("exported_users", userName, Consts.AuthorAPIKeyFile)
                    .ReadAllTextAsync();
                Assert.Equal(exported, apiKey);

            }
        }
    }
}
