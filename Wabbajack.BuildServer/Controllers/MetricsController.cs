using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Route("/metrics")]
    public class MetricsController : AControllerBase<MetricsController>
    {
        private SqlService _sql;

        public MetricsController(ILogger<MetricsController> logger, DBContext db, SqlService sql) : base(logger, db)
        {
            _sql = sql;
        }

        [HttpGet]
        [Route("{Subject}/{Value}")]
        public async Task<Result> LogMetricAsync(string Subject, string Value)
        {
            var date = DateTime.UtcNow;
            await Log(date, Subject, Value, Request.Headers[Consts.MetricsKeyHeader].FirstOrDefault());
            return new Result { Timestamp = date};
        }

        [Authorize]
        [HttpGet]
        [Route("transfer")]
        public async Task<string> Transfer()
        {
            var all_metrics = await Db.Metrics.AsQueryable().ToListAsync();
            await _sql.IngestAllMetrics(all_metrics);
            return "done";
        }

        private async Task Log(DateTime timestamp, string action, string subject, string metricsKey = null)
        {
            Logger.Log(LogLevel.Information, $"Log - {timestamp} {action} {subject} {metricsKey}");
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
