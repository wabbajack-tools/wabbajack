using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using WebSocketSharp;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/metrics")]
    public class MetricsController : ControllerBase
    {
        private SqlService _sql;
        private ILogger<MetricsController> _logger;

        public MetricsController(ILogger<MetricsController> logger, SqlService sql)
        {
            _sql = sql;
            _logger = logger;
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
            _logger.Log(LogLevel.Information, $"Log - {timestamp} {action} {subject} {metricsKey}");
            await _sql.IngestMetric(new Metric
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
