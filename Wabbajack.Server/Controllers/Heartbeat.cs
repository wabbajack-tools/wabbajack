using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common.StatusFeed;
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
        private ListValidator _listValidator;


        public Heartbeat(ILogger<Heartbeat> logger, SqlService sql, GlobalInformation globalInformation, QuickSync quickSync, ListValidator listValidator)
        {
            _globalInformation = globalInformation;
            _sql = sql;
            _logger = logger;
            _quickSync = quickSync;
            _listValidator = listValidator;
        }

        private const int MAX_LOG_SIZE = 128;
        private static List<string> Log  = new List<string>();
        private GlobalInformation _globalInformation;
        private SqlService _sql;
        private ILogger<Heartbeat> _logger;

        public static void AddToLog(IStatusMessage msg)
        {
            lock (Log)
            {
                Log.Add(msg.ToString());
                if (Log.Count > MAX_LOG_SIZE)
                    Log.RemoveAt(0);
            }
        }

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
                <li><b>{{$.Name}} - {{$.Time}} - {{$.MaxTime}}</b></li>
                {{else}}
                <li>{{$.Name}} - {{$.Time}} - {{$.MaxTime}}</li>
                {{/if}}
                {{/each}}
                </ul>


                <h3>Lists ({{lists.Length}}):</h3>
                <ul>
                {{each $.lists }}
                <li><a href='/lists/status/{{$.Name}}.html'>{{$.Name}}</a> - {{$.Time}}</li>
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
                lists = _listValidator.ValidationInfo.Select(s => new {Name = s.Key, Time = s.Value.ValidationTime})
                    .OrderBy(l => l.Name)
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
