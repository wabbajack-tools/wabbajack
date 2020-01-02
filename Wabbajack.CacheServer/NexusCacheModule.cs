using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nancy;
using Newtonsoft.Json;
using Wabbajack.CacheServer.DTOs;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.CacheServer
{
    public class NexusCacheModule : NancyModule
    {

        public NexusCacheModule() : base("/")
        {
            Get("/v1/games/{GameName}/mods/{ModID}/files/{FileID}.json", HandleFileID);
            Get("/v1/games/{GameName}/mods/{ModID}/files.json", HandleGetFiles);
            Get("/v1/games/{GameName}/mods/{ModID}.json", HandleModInfo);
            Get("/nexus_api_cache/{request}.json", HandleCacheCall);
            Get("/nexus_api_cache", ListCache);
            Get("/nexus_api_cache/update", UpdateCache);
            Get("/nexus_api_cache/ingest/{Folder}", HandleIngestCache);
        }

        class UpdatedMod
        {
            public long mod_id;
            public long latest_file_update;
            public long latest_mod_activity;
        }

        public async Task<object> UpdateCache(object arg)
        {
            var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
            
            var gameTasks = GameRegistry.Games.Values
                .Where(game => game.NexusName != null)
                .Select(async game =>
                {
                    return (game,
                        mods: await api.Get<List<UpdatedMod>>(
                            $"https://api.nexusmods.com/v1/games/{game.NexusName}/mods/updated.json?period=1m"));
                })
                .Select(async rTask =>
                {
                    var (game, mods) = await rTask;
                    return mods.Select(mod => new { game = game, mod = mod });
                }).ToList();

            Utils.Log($"Getting update list for {gameTasks.Count} games");

            var purge = (await Task.WhenAll(gameTasks))
                .SelectMany(i => i)
                .ToList();

            Utils.Log($"Found {purge.Count} updated mods in the last month");
            using (var queue = new WorkQueue())
            {
                var collected = await purge.Select(d =>
                {
                    var a = d.mod.latest_file_update.AsUnixTime();
                    // Mod activity could hide files
                    var b = d.mod.latest_mod_activity.AsUnixTime();

                    return new {Game = d.game.NexusName, Date = (a > b ? a : b), ModId = d.mod.mod_id.ToString()};
                }).PMap(queue, async t =>
                {
                    var resultA = await Server.Config.NexusModInfos.Connect().DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);
                    var resultB = await Server.Config.NexusModFiles.Connect().DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);
                    var resultC = await Server.Config.NexusFileInfos.Connect().DeleteManyAsync(f =>
                        f.Game == t.Game && f.ModId == t.ModId && f.LastCheckedUTC <= t.Date);

                    return resultA.DeletedCount + resultB.DeletedCount + resultC.DeletedCount;
                });

                Utils.Log($"Purged {collected.Sum()} cache entries");
            }

            return "Done";
        }

        private string ListCache(object arg)
        {
            Utils.Log($"{DateTime.Now} - List Cache");
            return String.Join("",
            Directory.EnumerateFiles(NexusApiClient.LocalCacheDir)
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTime)
                .Select(fi =>
                {
                    var decoded = Encoding.UTF8.GetString(Path.GetFileNameWithoutExtension(fi.Name).FromHex());
                    return $"{fi.LastWriteTime} \t {fi.Length.ToFileSizeString()} \t {decoded} \n";
                }));
        }

        private async Task<Response> HandleModInfo(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - Mod Info - {arg.GameName}/{arg.ModID}/");
            string gameName = arg.GameName;
            string modId = arg.ModId;
            var result = await Server.Config.NexusModInfos.Connect()
                .FindOneAsync(info => info.Game == gameName && info.ModId == modId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                var path = $"/v1/{gameName}/mods/{modId}.json";
                var body = await api.Get<ModInfo>(path);
                result = new NexusCacheData<ModInfo>
                {
                    Data = body, 
                    Path = path, 
                    Game = gameName,
                    ModId = modId
                };
                try
                {
                    await Server.Config.NexusModInfos.Connect().InsertOneAsync(result);
                }
                catch (MongoWriteException)
                {

                }

                method = "NOT_CACHED";
            }

            Response response = result.Data.ToJSON();
            response.Headers.Add("WABBAJACK_CACHE_FROM", method);
            response.ContentType = "application/json";
            return response;
        }

        private async Task<object> HandleFileID(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - File Info - {arg.GameName}/{arg.ModID}/{arg.FileID}");
            string gameName = arg.GameName;
            string modId = arg.ModId;
            string fileId = arg.FileId;
            var result = await Server.Config.NexusFileInfos.Connect()
                .FindOneAsync(info => info.Game == gameName && info.ModId == modId && info.FileId == fileId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                var path = $"/v1/{gameName}/mods/{modId}/files/{fileId}.json";
                var body = await api.Get<NexusFileInfo>(path);
                result = new NexusCacheData<NexusFileInfo>
                {
                    Data = body, 
                    Path = path, 
                    Game = gameName, 
                    ModId = modId,
                    FileId = fileId
                };
                try
                {
                    await Server.Config.NexusFileInfos.Connect().InsertOneAsync(result);
                }
                catch (MongoWriteException)
                {

                }

                method = "NOT_CACHED";
            }

            Response response = result.Data.ToJSON();
            response.Headers.Add("WABBAJACK_CACHE_FROM", method);
            response.ContentType = "application/json";
            return response;
        }

        private async Task<object> HandleGetFiles(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - Mod Files - {arg.GameName} {arg.ModID}");
            string gameName = arg.GameName;
            string modId = arg.ModId;
            var result = await Server.Config.NexusModFiles.Connect()
                .FindOneAsync(info => info.Game == gameName && info.ModId == modId);

            string method = "CACHED";
            if (result == null)
            {
                var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
                var path = $"/v1/{gameName}/mods/{modId}/files.json";
                var body = await api.Get<NexusApiClient.GetModFilesResponse>(path);
                result = new NexusCacheData<NexusApiClient.GetModFilesResponse>
                {
                    Data = body,
                    Path = path,
                    Game = gameName,
                    ModId = modId
                };
                try
                {
                    await Server.Config.NexusModFiles.Connect().InsertOneAsync(result);
                }
                catch (MongoWriteException)
                {

                }

                method = "NOT_CACHED";
            }

            Response response = result.Data.ToJSON();
            response.Headers.Add("WABBAJACK_CACHE_FROM", method);
            response.ContentType = "application/json";
            return response;
        }

        private async Task<string> HandleCacheCall(dynamic arg)
        {
            try
            {
                string param = (string)arg.request;
                var url = new Uri(Encoding.UTF8.GetString(param.FromHex()));

                var client = new HttpClient();
                var builder = new UriBuilder(url) {Host = "localhost", Port = Request.Url.Port ?? 8080, Scheme = "http"};
                client.DefaultRequestHeaders.Add("apikey", Request.Headers["apikey"]); 
                return await client.GetStringAsync(builder.Uri.ToString());
            }
            catch (Exception ex)
            {
                Utils.Log(ex.ToString());
                return "ERROR";
            }
        }

        private async Task<string> HandleIngestCache(dynamic arg)
        {
            int count = 0;
            int failed = 0;

            using (var queue = new WorkQueue())
            {
                await Directory.EnumerateFiles(Path.Combine(Server.Config.Settings.TempDir, (string)arg.Folder)).PMap(queue,
                    async file =>
                    {
                        Utils.Log($"Ingesting {file}");
                        if (!file.EndsWith(".json")) return;

                        var fileInfo = new FileInfo(file);
                        count++;

                        var url = new Url(
                            Encoding.UTF8.GetString(Path.GetFileNameWithoutExtension(file).FromHex()));
                        var split = url.Path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                        try
                        {
                            switch (split.Length)
                            {
                                case 5 when split[3] == "mods":
                                {
                                    var body = file.FromJSON<ModInfo>();

                                    var payload = new NexusCacheData<ModInfo>();
                                    payload.Data = body;
                                    payload.Game = split[2];
                                    payload.Path = url.Path;
                                    payload.ModId = body.mod_id;
                                    payload.LastCheckedUTC = fileInfo.LastWriteTimeUtc;

                                    try
                                    {
                                        await Server.Config.NexusModInfos.Connect().InsertOneAsync(payload);
                                    }
                                    catch (MongoWriteException ex)
                                    {

                                    }

                                    break;
                                }
                                case 6 when split[5] == "files.json":
                                {
                                    var body = file.FromJSON<NexusApiClient.GetModFilesResponse>();
                                    var payload = new NexusCacheData<NexusApiClient.GetModFilesResponse>();
                                    payload.Path = url.Path;
                                    payload.Data = body;
                                    payload.Game = split[2];
                                    payload.ModId = split[4];
                                    payload.LastCheckedUTC = fileInfo.LastWriteTimeUtc;

                                        try
                                        {
                                        await Server.Config.NexusModFiles.Connect().InsertOneAsync(payload);
                                    }
                                    catch (MongoWriteException ex)
                                    {

                                    }

                                    break;
                                }
                                case 7 when split[5] == "files":
                                {
                                    var body = file.FromJSON<NexusFileInfo>();
                                    var payload = new NexusCacheData<NexusFileInfo>();
                                    payload.Data = body;
                                    payload.Path = url.Path;
                                    payload.Game = split[2];
                                    payload.FileId = Path.GetFileNameWithoutExtension(split[6]);
                                    payload.ModId = split[4];
                                    payload.LastCheckedUTC = fileInfo.LastWriteTimeUtc;

                                        try
                                        {
                                        await Server.Config.NexusFileInfos.Connect().InsertOneAsync(payload);
                                    }
                                    catch (MongoWriteException ex)
                                    {

                                    }

                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                        }
                    });
            }

            return $"Inserted {count} caches, {failed} failed";
        }
    }
}
