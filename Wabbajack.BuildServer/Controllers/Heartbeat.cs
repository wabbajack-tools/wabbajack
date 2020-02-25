using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
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

        public Heartbeat(ILogger<Heartbeat> logger, DBContext db) : base(logger, db)
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
        public async Task<TimeSpan> GetHeartbeat()
        {
            return DateTime.Now - _startTime;
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
