using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/metrics")]
    public class MetricsController : AControllerBase<MetricsController>
    {
        public MetricsController(ILogger<MetricsController> logger, DBContext db) : base(logger, db)
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
            await Db.Metrics.InsertOneAsync(new Metric
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
