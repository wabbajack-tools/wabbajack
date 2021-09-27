using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Server;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/heartbeat")]
    public class Heartbeat : ControllerBase
    {
        static Heartbeat()
        {
            _startTime = DateTime.Now;
            
        }
        private static DateTime _startTime;
        
        private QuickSync _quickSync;


        public Heartbeat(ILogger<Heartbeat> logger, SqlService sql, GlobalInformation globalInformation, QuickSync quickSync)
        {
            _globalInformation = globalInformation;
            _sql = sql;
            _logger = logger;
            _quickSync = quickSync;
        }

        private const int MAX_LOG_SIZE = 128;
        private static List<string> Log  = new();
        private GlobalInformation _globalInformation;
        private SqlService _sql;
        private ILogger<Heartbeat> _logger;

        [HttpGet]
        public async Task<IActionResult> GetHeartbeat()
        {
            return Ok(new HeartbeatResult
            {
                Uptime = DateTime.Now - _startTime,
                LastNexusUpdate = _globalInformation.TimeSinceLastNexusSync,
            });
        }
        
        private static readonly Func<object, string> HandleGetReport = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <h2>Server Status</h2>

                <h3>Service Overview ({{services.Length}}):</h3>
                <ul>
                {{each $.services }}
                {{if $.IsLate}}
                <li><a href='/heartbeat/report/services/{{$.Name}}.html'><b>{{$.Name}} - {{$.Time}} - {{$.MaxTime}}</b></a></li>
                {{else}}
                <li><a href='/heartbeat/report/services/{{$.Name}}.html'>{{$.Name}} - {{$.Time}} - {{$.MaxTime}}</a></li>
                {{/if}}
                {{/each}}
                </ul>


                <h3>Lists ({{lists.Length}}):</h3>
                <ul>
                {{each $.lists }}
                <li><a href='/lists/status/{{$.Name}}.html'>{{$.Name}}</a> - {{$.Time}} {{$.FailMessage}}</li>
                {{/each}}
                </ul>
            </body></html>
        ");

        [HttpGet("report")]
        public async Task<ContentResult> Report()
        {
            var response = HandleGetReport(new
            {
                services = (await _quickSync.Report())
                    .Select(s => new {Name = s.Key.Name, Time = s.Value.LastRunTime, MaxTime = s.Value.Delay, IsLate = s.Value.LastRunTime > s.Value.Delay})
                    .OrderBy(s => s.Name)
                    .ToArray(),

            });
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int) HttpStatusCode.OK,
                Content = response
            };
            
        }
        
        private static readonly Func<object, string> HandleGetServiceReport = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <h2>Service Status: {{Name}} {{TimeSinceLastRun}}</h2>

                <h3>Service Overview ({{ActiveWorkQueue.Length}}):</h3>
                <ul>
                {{each $.ActiveWorkQueue }}
                <li>{{$.Name}} {{$.Time}}</li>
                {{/each}}
                </ul>

            </body></html>
        ");

        [HttpGet("report/services/{serviceName}.html")]
        public async Task<ContentResult> ReportServiceStatus(string serviceName)
        {
            var services = await _quickSync.Report();
            var info = services.First(kvp => kvp.Key.Name == serviceName);

            var response = HandleGetServiceReport(new
            {
                Name = info.Key.Name,
                TimeSinceLastRun = DateTime.UtcNow - info.Value.LastRunTime,
                ActiveWorkQueue = info.Value.ActiveWork.Select(p => new
                {
                    Name = p.Item1,
                    Time = DateTime.UtcNow - p.Item2
                }).OrderByDescending(kp => kp.Time)
                    .ToArray()
                
            });
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int) HttpStatusCode.OK,
                Content = response
            };
        }
    }
}
