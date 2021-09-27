using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.NexusApi.DTOs;
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
        private SqlService _sql;
        private ILogger<NexusCache> _logger;
        private readonly NexusApi _api;

        public NexusCache(ILogger<NexusCache> logger, SqlService sql, AppSettings settings, NexusApi api)
        {
            _settings = settings;
            _sql = sql;
            _logger = logger;
            _api = api;
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
            var game = GameRegistry.GetByNexusName(GameName)!;
            var result = await _sql.GetNexusModInfoString(game.Game, ModId);
            
            string method = "CACHED";
            if (result == null)
            {
                var (result2, headers) = await _api.ModInfo(game.NexusName!, ModId);
                result = result2;
                await _sql.AddNexusModInfo(game.Game, ModId, result.UpdatedTime, result);

                
                method = "NOT_CACHED";
            }

            Response.Headers.Add("x-cache-result", method);
            return result;
        }

        [HttpGet]
        [Route("{GameName}/mods/{ModId}/files.json")]
        public async Task<ModFiles> GetModFiles(string GameName, long ModId)
        {
            //_logger.Log(LogLevel.Information, $"{GameName} {ModId}");
            var game = GameRegistry.GetByNexusName(GameName)!;
            var result = await _sql.GetModFiles(game!.Game, ModId);

            string method = "CACHED";
            if (result == null)
            {
                var (result2, _) = await _api.ModFiles(game.NexusName!, ModId);
                result = result2;

                var date = result.Files.Select(f => f.UploadedTime).OrderByDescending(o => o).FirstOrDefault();
                date = date == default ? DateTime.UtcNow : date;
                await _sql.AddNexusModFiles(game.Game, ModId, date, result);

                method = "NOT_CACHED";
            }

            Response.Headers.Add("x-cache-result", method);
            return result;
        }
        
        [HttpGet]
        [Route("{GameName}/mods/{ModId}/files/{FileId}.json")]
        public async Task<ActionResult<ModFile>> GetModFile(string GameName, long ModId, long FileId)
        {
            try
            {
                var game = GameRegistry.GetByNexusName(GameName)!;
                var result = await _sql.GetModFile(game.Game, ModId, FileId);

                string method = "CACHED";
                if (result == null)
                {
                    var (result2, _) = await _api.FileInfo(game.NexusName, ModId, FileId);
                    result = result2;

                    
                    var date = result.UploadedTime;
                    date = date == default ? DateTime.UtcNow : date;
                    await _sql.AddNexusModFile(game.Game, ModId, FileId, date, result);

                    method = "NOT_CACHED";
                }


                Response.Headers.Add("x-cache-result", method);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Unable to find mod file {GameName} {ModId}, {FileId}", GameName, ModId, FileId);
                return NotFound();
            }
        }
    }
}
