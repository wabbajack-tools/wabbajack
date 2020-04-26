using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.BuildServer.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("/v1/games/")]
    public class NexusCache : AControllerBase<NexusCache>
    {
        private AppSettings _settings;
        private static long CachedCount = 0;
        private static long ForwardCount = 0;

        public NexusCache(ILogger<NexusCache> logger, SqlService sql, AppSettings settings) : base(logger, sql)
        {
            _settings = settings;
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
            var result = await SQL.GetNexusModInfoString(game, ModId);
            
            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                result = await api.GetModInfo(game, ModId, false);
                await SQL.AddNexusModInfo(game, ModId, DateTime.UtcNow, result);

                
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
            var game = GameRegistry.GetByFuzzyName(GameName).Game;
            var result = await SQL.GetModFiles(game, ModId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                result = await api.GetModFiles(game, ModId, false);
                await SQL.AddNexusModFiles(game, ModId, DateTime.UtcNow, result);

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

        private class NexusIngestHeader
        {
            public List<NexusCacheData<ModInfo>> ModInfos { get; set; }
            public List<NexusCacheData<NexusFileInfo>> FileInfos { get; set; }
            public List<NexusCacheData<NexusApiClient.GetModFilesResponse>> ModFiles { get; set; }
        }
        
        [HttpGet]
        [Route("/nexus_cache/ingest")]
        [Authorize]
        public async Task<IActionResult> IngestNexusFile()
        {
            long totalRows = 0;
            
            var dataPath = @"nexus_export.json".RelativeTo(_settings.TempPath);

            var data = JsonConvert.DeserializeObject<NexusIngestHeader>(await dataPath.ReadAllTextAsync());

            foreach (var record in data.ModInfos)
            {
                if (!GameRegistry.TryGetByFuzzyName(record.Game, out var game)) continue;

                await SQL.AddNexusModInfo(game.Game, record.ModId,
                    record.LastCheckedUTC, record.Data);
                totalRows += 1;
            }
            
            foreach (var record in data.FileInfos)
            {
                if (!GameRegistry.TryGetByFuzzyName(record.Game, out var game)) continue;
                
                await SQL.AddNexusFileInfo(game.Game, record.ModId,
                    long.Parse(record.FileId),
                    record.LastCheckedUTC, record.Data);
                totalRows += 1;
            }
            
            foreach (var record in data.ModFiles)
            {
                if (!GameRegistry.TryGetByFuzzyName(record.Game, out var game)) continue;

                await SQL.AddNexusModFiles(game.Game, record.ModId,
                    record.LastCheckedUTC, record.Data);
                totalRows += 1;
            }

            return Ok(totalRows);
        }

        [HttpGet]
        [Route("/nexus_cache/stats")]
        public async Task<IActionResult> NexusCacheStats()
        {
            return Ok(new ClientAPI.NexusCacheStats
            {
                CachedCount = CachedCount,
                ForwardCount = ForwardCount,
                CacheRatio = (double)CachedCount / (ForwardCount == 0 ? 1 : ForwardCount)
            });
        }
        
    }
}
