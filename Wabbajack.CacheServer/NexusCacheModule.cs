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

        private object HandleModInfo(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - Mod Info - {arg.GameName}/{arg.ModID}/");
            var api = new NexusApiClient(Request.Headers["apikey"].FirstOrDefault());
            return api.GetModInfo(GameRegistry.GetByNexusName((string)arg.GameName).Game, (string)arg.ModID).ToJSON();
        }

        private object HandleGetFiles(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - File Info - {arg.GameName}/{arg.ModID}/{arg.FileID}");
            var api = new NexusApiClient(Request.Headers["apikey"].FirstOrDefault());
            return api.GetFileInfo(new NexusDownloader.State
            {
                GameName = arg.GameName,
                ModID = arg.ModID,
                FileID = arg.FileID
            }).ToJSON();
        }

        private object HandleFileID(dynamic arg)
        {
            Utils.Log($"{DateTime.Now} - Mod Files - {arg.GameName} {arg.ModID}");
            var api = new NexusApiClient(Request.Headers["apikey"].FirstOrDefault());
            return api.GetModFiles(GameRegistry.GetByNexusName(arg.GameName), arg.ModID).ToJSON();
        }

        private string HandleCacheCall(dynamic arg)
        {
            string param = (string)arg.request;
            var url = new Uri(Encoding.UTF8.GetString(param.FromHex()));
            var path = Path.Combine(NexusApiClient.LocalCacheDir, arg.request + ".json");

            if (!File.Exists(path))
            {
                Utils.Log($"{DateTime.Now} - Not Cached - {url}");
                var client = new HttpClient();
                var builder = new UriBuilder(url) {Host = "localhost", Port = Request.Url.Port ?? 80};
                client.DefaultRequestHeaders.Add("apikey", Request.Headers["apikey"]);
                return client.GetStringSync(builder.Uri.ToString());
            }

            Utils.Log($"{DateTime.Now} - From Cached - {url}");
            return File.ReadAllText(path);

        }
    }
}
