using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers
{
    //[Authorize]
    [ApiController]
    [Authorize(Roles = "User")]
    [Route("/v1/games/")]
    public class NexusCache : ControllerBase
    {
        private AppSettings _settings;
        private static long CachedCount = 0;
        private static long ForwardCount = 0;
        private SqlService _sql;
        private ILogger<NexusCache> _logger;
        private NexusKeyMaintainance _keys;

        public NexusCache(ILogger<NexusCache> logger, SqlService sql, AppSettings settings, NexusKeyMaintainance keys)
        {
            _settings = settings;
            _sql = sql;
            _logger = logger;
            _keys = keys;
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
                var api = await GetClient();
                result = await api.GetModInfo(game, ModId, false);
                await _sql.AddNexusModInfo(game, ModId, result.updated_time, result);

                
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

        private async Task<NexusApiClient> GetClient()
        {
            var key = Request.Headers["apikey"].FirstOrDefault();
            if (key == null)
                return await _keys.GetClient();

            if (await _sql.HaveKey(key))
                return await NexusApiClient.Get(key);

            var client = await NexusApiClient.Get(key);
            var (daily, hourly) = await client.GetRemainingApiCalls();
            await _sql.SetNexusAPIKey(key, daily, hourly);
            return client;
        }

        [HttpGet]
        [Route("{GameName}/mods/{ModId}/files.json")]
        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(string GameName, long ModId)
        {
            //_logger.Log(LogLevel.Information, $"{GameName} {ModId}");
            var game = GameRegistry.GetByFuzzyName(GameName).Game;
            var result = await _sql.GetModFiles(game, ModId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await GetClient();
                var permission = HTMLInterface.GetUploadPermissions(game, ModId);
                try
                {
                    result = await api.GetModFiles(game, ModId, false);
                }
                catch (HttpException ex)
                {
                    if (ex.Code == 403)
                        result = new NexusApiClient.GetModFilesResponse {files = new List<NexusFileInfo>()};
                    else
                        throw;
                }

                var date = result.files.Select(f => f.uploaded_time).OrderByDescending(o => o).FirstOrDefault();
                date = date == default ? DateTime.UtcNow : date;
                await _sql.AddNexusModFiles(game, ModId, date, result);
                await _sql.SetNexusPermission(game, ModId, await permission);

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
        [Authorize(Roles ="Author")]
        [Route("/purge_nexus_cache/{ModId}")]
        public async Task<IActionResult> PurgeNexusCache(long ModId)
        {
            _logger.LogInformation($"Purging nexus cache for {ModId}");
            await _sql.PurgeNexusCache(ModId);
            return Ok("Purged");
        }
    }
}
