using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Lib;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class MetricsTests : ABuildServerSystemTest
    {
        public MetricsTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanSendAndGetMetrics()
        {
            var action = "action_" + Guid.NewGuid().ToString();
            var subject = "subject_" + Guid.NewGuid().ToString();
            await Metrics.Send(action, subject);

            var sql = Fixture.GetService<SqlService>();
            var conn = await sql.Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>("SELECT Subject FROM dbo.Metrics WHERE Action = @Action",
                new {Action = action});

            Assert.Equal(subject, result);


            using var response = await _client.GetAsync(MakeURL($"metrics/report/{action}"));
            Assert.Equal(TimeSpan.FromHours(1), response.Headers.CacheControl.MaxAge);
            // we'll just make sure this doesn't error, with limited data that's about all we can do atm
            
            using var totalInstalls = await _client.GetAsync(MakeURL($"metrics/total_installs.html"));
            Assert.True(totalInstalls.IsSuccessStatusCode);
            
            using var totalUniqueInstalls = await _client.GetAsync(MakeURL($"metrics/total_unique_installs.html"));
            Assert.True(totalUniqueInstalls.IsSuccessStatusCode);

            using var dumpResponse = await _client.GetAsync(MakeURL("metrics/dump.json"));
            Assert.True(dumpResponse.IsSuccessStatusCode);
            var data = await dumpResponse.Content.ReadAsStringAsync();
            Assert.NotEmpty(data);

            var cache = Fixture.GetService<MetricsKeyCache>();
            Assert.True(await cache.KeyCount() > 0);
        }
    }
}
