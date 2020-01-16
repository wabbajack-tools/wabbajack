using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

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
    }
}
