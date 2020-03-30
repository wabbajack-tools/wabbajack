using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Wabbajack.BuildServer.Model.Models;
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

        public Heartbeat(ILogger<Heartbeat> logger, DBContext db, SqlService sql) : base(logger, db, sql)
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

        [HttpPost("export_inis")]
        [Authorize]
        public async Task<IActionResult> ExportInis()
        {
            if (!Directory.Exists("exported_inis"))
                Directory.CreateDirectory("exported_inis");

            var loaded = 0;
            foreach (var ini in await Db.DownloadStates.AsQueryable().ToListAsync())
            {
                var file = Path.Combine("exported_inis", ini.Hash.FromBase64().ToHex() + ".ini");
                Alphaleonis.Win32.Filesystem.File.WriteAllLines(file, ini.State.GetMetaIni());
                loaded += 1;
            }

            return Ok(loaded);
        }
    }
}
