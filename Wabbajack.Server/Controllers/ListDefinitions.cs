using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    [Route("/list_definitions")]
    public class ListDefinitions : ControllerBase
    {
        private ILogger<ListDefinitions> _logger;
        private SqlService _sql;
        private DiscordWebHook _discord;

        public ListDefinitions(ILogger<ListDefinitions> logger, SqlService sql, DiscordWebHook discord)
        {
            _logger = logger;
            _sql = sql;
            _discord = discord;
        }


        [Route("ingest")]
        [Authorize(Roles = "User")]
        [HttpPost]
        public async Task<IActionResult> PostIngest()
        {
            var user = Request.Headers[Consts.MetricsKeyHeader].First();
            _logger.Log(LogLevel.Information, $"Ingesting Modlist Definition for {user}");
            var modlistBytes = await Request.Body.ReadAllAsync();
            var modlist = new MemoryStream(modlistBytes).FromJson<ModList>();

            var file = AbsolutePath.EntryPoint.Combine("mod_list_definitions")
                .Combine($"{user}_{DateTime.UtcNow.ToFileTimeUtc()}.json");
            file.Parent.CreateDirectory();
            await using var stream = await file.OpenWrite();
            modlist.ToJson(stream);
            _logger.Log(LogLevel.Information, $"Done Ingesting Modlist Definition for {user}");
            return Accepted(0);
        }
        
    }
}
