using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using Nancy.Helpers;
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
        }

        public async Task<object> UpdateCache(object arg)
        {
            var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
            await api.ClearUpdatedModsInCache();
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

        private async Task<object> HandleModInfo(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - Mod Info - {arg.GameName}/{arg.ModID}/");
            var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
            return api.GetModInfo(GameRegistry.GetByNexusName((string)arg.GameName).Game, (string)arg.ModID).ToJSON();
        }

        private async Task<object> HandleFileID(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - File Info - {arg.GameName}/{arg.ModID}/{arg.FileID}");
            var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
            return api.GetFileInfo(new NexusDownloader.State
            {
                GameName = arg.GameName,
                ModID = arg.ModID,
                FileID = arg.FileID
            }).ToJSON();
        }

        private async Task<object> HandleGetFiles(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - Mod Files - {arg.GameName} {arg.ModID}");
            var api = await NexusApiClient.Get(Request.Headers["apikey"].FirstOrDefault());
            return api.GetModFiles(GameRegistry.GetByNexusName((string)arg.GameName).Game, (int)arg.ModID).ToJSON();
        }

        private async Task<string> HandleCacheCall(dynamic arg)
        {
            try
            {
                string param = (string)arg.request;
                var url = new Uri(Encoding.UTF8.GetString(param.FromHex()));
                var path = Path.Combine(NexusApiClient.LocalCacheDir, arg.request + ".json");

                if (!File.Exists(path))
                {
                    Utils.Log($"{DateTime.Now} - Not Cached - {url}");
                    var client = new HttpClient();
                    var builder = new UriBuilder(url) {Host = "localhost", Port = Request.Url.Port ?? 8080, Scheme = "http"};
                    client.DefaultRequestHeaders.Add("apikey", Request.Headers["apikey"]);
                    await client.GetStringAsync(builder.Uri.ToString());
                    if (!File.Exists(path))
                    {
                        Utils.Log($"Still not cached : {path}");
                        throw new InvalidDataException("Invalid Data");
                    }

                    Utils.Log($"Is Now Cached : {path}");

                }

                Utils.Log($"{DateTime.Now} - From Cached - {url}");
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Utils.Log(ex.ToString());
                return "ERROR";
            }
        }
    }
}
