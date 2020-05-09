using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Io;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.BuildServer.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("/v1/games/")]
    public class NexusCache : ControllerBase
    {
        private AppSettings _settings;
        private static long CachedCount = 0;
        private static long ForwardCount = 0;
        private SqlService _sql;
        private ILogger<NexusCache> _logger;

        public NexusCache(ILogger<NexusCache> logger, SqlService sql, AppSettings settings)
        {
            _settings = settings;
            _sql = sql;
            _logger = logger;
        }

        /// <summary>
        ///     Looks up the mod details for a given Gamename/ModId pair. If the entry is not found in the cache it will
        ///     be requested from the server (using the caller's Nexus API key if provided).
        /// </summary>
        /// <param name="db"></param>
        /// <param name="GameName">The Nexus game name</param>
        /// <param name="ModId">The Nexus mod id</param>
        /// <returns>A Mod Info result</returns>
        [HttpGet]
        [Route("{GameName}/mods/{ModId}.json")]
        public async Task<ModInfo> GetModInfo(string GameName, long ModId)
        {
            var game = GameRegistry.GetByFuzzyName(GameName).Game;
            var result = await _sql.GetNexusModInfoString(game, ModId);
            
            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                result = await api.GetModInfo(game, ModId, false);
                await _sql.AddNexusModInfo(game, ModId, DateTime.UtcNow, result);

                
                method = "NOT_CACHED";
                Interlocked.Increment(ref ForwardCount);
            }
            else
            {
                Interlocked.Increment(ref CachedCount);
            }

            Response.Headers.Add("x-cache-result", method);
            return result;
        }

        [HttpGet]
        [Route("{GameName}/mods/{ModId}/files.json")]
        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(string GameName, long ModId)
        {
            _logger.Log(LogLevel.Information, $"{GameName} {ModId}");
            var game = GameRegistry.GetByFuzzyName(GameName).Game;
            var result = await _sql.GetModFiles(game, ModId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                result = await api.GetModFiles(game, ModId, false);
                await _sql.AddNexusModFiles(game, ModId, DateTime.UtcNow, result);

                method = "NOT_CACHED";
                Interlocked.Increment(ref ForwardCount);
            }
            else
            {
                Interlocked.Increment(ref CachedCount);
            }
            Response.Headers.Add("x-cache-result", method);
            return result;
        }
    }
}
