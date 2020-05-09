using System;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
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


            var report = await _client.GetJsonAsync<MetricResult[]>(MakeURL($"metrics/report/{action}"));
            // we'll just make sure this doesn't error, with limited data that's about all we can do atm
           
        }
    }
}
