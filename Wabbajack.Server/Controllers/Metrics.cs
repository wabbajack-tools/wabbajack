using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.Server;
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
        [Route("{subject}/{value}")]
        public async Task<Result> LogMetricAsync(string subject, string value)
        {
            var date = DateTime.UtcNow;
            await Log(date, subject, value, Request.Headers[Consts.MetricsKeyHeader].FirstOrDefault());
            return new Result { Timestamp = date};
        }

        [HttpGet]
        [Route("report/{subject}")]
        public async Task<IActionResult> MetricsReport(string subject)
        {
            var results = (await _sql.MetricsReport(subject))
                .GroupBy(m => m.Subject)
                .Select(g => new MetricResult
                {
                    SeriesName = g.Key,
                    Labels = g.Select(m => m.Date.ToString(CultureInfo.InvariantCulture)).ToList(),
                    Values = g.Select(m => m.Count).ToList()
                });
            return Ok(results.ToList());
        }

        [HttpGet]
        [Route("badge/{name}/total_installs_badge.json")]
        public async Task<IActionResult> TotalInstallsBadge(string name)
        {
            var results = await _sql.TotalInstalls(name);

            Response.ContentType = "application/json";
           
            return Ok(results == 0 
                ? new Badge($"Modlist {name} not found!", "Error") {color = "red"} 
                : new Badge("Installations: ", $"{results}") {color = "green"});
        }

        [HttpGet]
        [Route("badge/{name}/unique_installs_badge.json")]
        public async Task<IActionResult> UniqueInstallsBadge(string name)
        {
            var results = await _sql.UniqueInstalls(name);

            Response.ContentType = "application/json";

            return Ok(results == 0
                ? new Badge($"Modlist {name} not found!", "Error") {color = "red"}
                : new Badge("Installations: ", $"{results}"){color = "green"}) ;
    }

        private static readonly Func<object, string> ReportTemplate = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <h2>Tar Report for {{$.key}}</h2>
                <h3>Ban Status: {{$.status}}</h3>
                <table>
                {{each $.log }}
                <tr>
                <td>{{$.Timestamp}}</td>
                <td>{{$.Path}}</td>
                <td>{{$.Key}}</td>
                </tr>
                {{/each}}
                </table>
            </body></html>
        ");
        
        [HttpGet]
        [Route("tarlog/{key}")]
        public async Task<IActionResult> TarLog(string key)
        {
            var isTarKey = await _sql.IsTarKey(key);

            List<(DateTime, string, string)> report = new List<(DateTime, string, string)>();
            
            if (isTarKey) report = await _sql.FullTarReport(key);
            
            var response = ReportTemplate(new
            {
                key = key,
                status = isTarKey ? "BANNED" : "NOT BANNED",
                log = report.Select(entry => new
                {
                    Timestamp = entry.Item1,
                    Path = entry.Item2,
                    Key = entry.Item3
                }).ToList()
            });
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int) HttpStatusCode.OK,
                Content = response
            };
            
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
