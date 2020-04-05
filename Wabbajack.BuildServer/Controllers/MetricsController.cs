using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/metrics")]
    public class MetricsController : AControllerBase<MetricsController>
    {
        public MetricsController(ILogger<MetricsController> logger, SqlService sql) : base(logger, sql)
        {
        }

        [HttpGet]
        [Route("{Subject}/{Value}")]
        public async Task<Result> LogMetricAsync(string Subject, string Value)
        {
            var date = DateTime.UtcNow;
            await Log(date, Subject, Value, Request.Headers[Consts.MetricsKeyHeader].FirstOrDefault());
            return new Result { Timestamp = date};
        }

        private async Task Log(DateTime timestamp, string action, string subject, string metricsKey = null)
        {
            Logger.Log(LogLevel.Information, $"Log - {timestamp} {action} {subject} {metricsKey}");
            await SQL.IngestMetric(new Metric
            {
                Timestamp = timestamp, Action = action, Subject = subject, MetricsKey = metricsKey
            });
        }

        public class Result
        {
            public DateTime Timestamp { get; set; }
        }
    }
}
