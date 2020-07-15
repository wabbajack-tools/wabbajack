using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Authorize(Roles = "User")]
    public class VirusScan : ControllerBase
    {
        private ILogger<VirusScan> _logger;
        private SqlService _sql;

        public VirusScan(ILogger<VirusScan> logger, SqlService sql)
        {
            _logger = logger;
            _sql = sql;
        }

        [HttpPost]
        [Route("/virus_scan")]
        public async Task<IActionResult> CheckFile()
        {
            var result = await VirusScanner.ScanStream(Request.Body);
            await _sql.AddVirusResult(result.Item1, result.Item2);
            return Ok(result.Item2.ToString());
        }

        [HttpGet]
        [Route("/virus_scan/{hashAsHex}")]
        public async Task<IActionResult> CheckHash(string hashAsHex)
        {
            var result = await _sql.FindVirusResult(Hash.FromHex(hashAsHex));
            if (result == null)
            {
                return NotFound();
            }

            return Ok(result.ToString());
        }
        
    }
}
