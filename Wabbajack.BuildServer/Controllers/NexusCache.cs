using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Wabbajack.BuildServer.Models;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.BuildServer.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("/v1/games/")]
    public class NexusCache : AControllerBase<NexusCache>
    {
        public NexusCache(ILogger<NexusCache> logger, DBContext db) : base(logger, db)
        {
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
        public async Task<ModInfo> GetModInfo(string GameName, string ModId)
        {
            var result = await Db.NexusModInfos.FindOneAsync(info => info.Game == GameName && info.ModId == ModId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                var path = $"/v1/games/{GameName}/mods/{ModId}.json";
                var body = await api.Get<ModInfo>(path);
                result = new NexusCacheData<ModInfo> {Data = body, Path = path, Game = GameName, ModId = ModId};
                try
                {
                    await Db.NexusModInfos.InsertOneAsync(result);
                }
                catch (MongoWriteException)
                {
                }

                method = "NOT_CACHED";
            }

            Response.Headers.Add("x-cache-result", method);
            return result.Data;
        }

        [HttpGet]
        [Route("{GameName}/mods/{ModId}/files.json")]
        public async Task<NexusApiClient.GetModFilesResponse> GetModFiles(string GameName, string ModId)
        {
            var result = await Db.NexusModFiles.FindOneAsync(info => info.Game == GameName && info.ModId == ModId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                var path = $"/v1/games/{GameName}/mods/{ModId}/files.json";
                var body = await api.Get<NexusApiClient.GetModFilesResponse>(path);
                result = new NexusCacheData<NexusApiClient.GetModFilesResponse>
                {
                    Data = body, Path = path, Game = GameName, ModId = ModId
                };
                try
                {
                    await Db.NexusModFiles.InsertOneAsync(result);
                }
                catch (MongoWriteException)
                {
                }

                method = "NOT_CACHED";
            }

            Response.Headers.Add("x-cache-result", method);
            return result.Data;
        }

        [HttpGet]
        [Route("{GameName}/mods/{ModId}/files/{FileId}.json")]
        public async Task<object> GetFileInfo(string GameName, string ModId, string FileId)
        {
            var result = await Db.NexusFileInfos.FindOneAsync(info =>
                info.Game == GameName && info.ModId == ModId && info.FileId == FileId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                var path = $"/v1/games/{GameName}/mods/{ModId}/files/{FileId}.json";
                var body = await api.Get<NexusFileInfo>(path);
                result = new NexusCacheData<NexusFileInfo>
                {
                    Data = body,
                    Path = path,
                    Game = GameName,
                    ModId = ModId,
                    FileId = FileId
                };
                try
                {
                    await Db.NexusFileInfos.InsertOneAsync(result);
                }
                catch (MongoWriteException)
                {
                }

                method = "NOT_CACHED";
            }

            Response.Headers.Add("x-cache-method", method);
            return result.Data;
        }
    }
}
