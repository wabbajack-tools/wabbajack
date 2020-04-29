using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Cms;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/heartbeat")]
    public class Heartbeat : AControllerBase<Heartbeat>
    {
        static Heartbeat()
        {
            _startTime = DateTime.Now;
            
        }
        private static DateTime _startTime;

        public Heartbeat(ILogger<Heartbeat> logger, SqlService sql) : base(logger, sql)
        {
        }

        private const int MAX_LOG_SIZE = 128;
        private static List<string> Log  = new List<string>();
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
                LastNexusUpdate = DateTime.Now - GetNexusUpdatesJob.LastNexusSync,
                LastListValidation = DateTime.UtcNow - ListValidation.SummariesLastChecked
            });
        }

        [JsonName("HeartbeatResult")]
        public class HeartbeatResult
        {
            public TimeSpan Uptime { get; set; }
            public TimeSpan LastNexusUpdate { get; set; }
            
            public TimeSpan LastListValidation { get; set; }
        }

        [HttpGet("only-authenticated")]
        [Authorize]
        public IActionResult OnlyAuthenticated()
        {
            var message = $"Hello from {nameof(OnlyAuthenticated)}";
            return new ObjectResult(message);
        }

        [HttpGet("logs")]
        [Authorize]
        public IActionResult GetLogs()
        {
            string[] lst;
            lock (Log)
            {
                lst = Log.ToArray();
            }
            return Ok(string.Join("\n", lst));
        }
    }
}
