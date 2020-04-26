using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;
using Wabbajack.Lib;
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
        public async Task CanSendMetrics()
        {
            var action = "action_" + Guid.NewGuid().ToString();
            var subject = "subject_" + Guid.NewGuid().ToString();
            await Metrics.Send(action, subject);

            var sql = Fixture.GetService<SqlService>();
            var conn = await sql.Open();
            var result = await conn.QueryFirstOrDefaultAsync<string>("SELECT Subject FROM dbo.Metrics WHERE Action = @Action",
                new {Action = action});

            Assert.Equal(subject, result);
        }

        [Fact]
        public async Task CanLoadMetricsFromSQL()
        {
            var sql = Fixture.GetService<SqlService>();
            var results = await sql.MetricsReport("finish_install");
        }
    }
}
