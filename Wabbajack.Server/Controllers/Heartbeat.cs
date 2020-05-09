using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Server;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

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

        public Heartbeat(ILogger<Heartbeat> logger, SqlService sql, GlobalInformation globalInformation)
        {
            _globalInformation = globalInformation;
            _sql = sql;
            _logger = logger;
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



    }
}
