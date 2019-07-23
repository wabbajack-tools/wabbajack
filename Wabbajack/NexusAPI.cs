using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using WebSocketSharp;

namespace Wabbajack
{
    class NexusAPI
    {
        public static string GetNexusAPIKey()
        {
            FileInfo fi = new FileInfo("nexus.key_cache");
            if (fi.Exists && fi.LastWriteTime > DateTime.Now.AddHours(-12))
            {
                return File.ReadAllText("nexus.key_cache");
            }

            var guid = Guid.NewGuid();
            var _websocket = new WebSocket("wss://sso.nexusmods.com")
            {
                SslConfiguration = {
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                }
            };

            TaskCompletionSource<string> api_key = new TaskCompletionSource<string>();
            _websocket.OnMessage += (sender, msg) =>
            {
                api_key.SetResult(msg.Data);
                return;
            };

            _websocket.Connect();
            _websocket.Send("{\"id\": \"" + guid + "\", \"appid\": \"Wabbajack\"}");

            Process.Start($"https://www.nexusmods.com/sso?id={guid}&application=Wabbajack");

            api_key.Task.Wait();
            var result = api_key.Task.Result;
            File.WriteAllText("nexus.key_cache", result);
            return result;
        }
        class DownloadLink
        {
            public string URI { get; set; }
        }
        private static HttpClient BaseNexusClient(string apikey)
        {
            var _baseHttpClient = new HttpClient();

            _baseHttpClient.DefaultRequestHeaders.Add("User-Agent", Consts.UserAgent);
            _baseHttpClient.DefaultRequestHeaders.Add("apikey", apikey);
            _baseHttpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _baseHttpClient.DefaultRequestHeaders.Add("Application-Name", "Wabbajack");
            _baseHttpClient.DefaultRequestHeaders.Add("Application-Version", $"{Assembly.GetEntryAssembly().GetName().Version}");
            return _baseHttpClient;
        }

        public static string GetNexusDownloadLink(NexusMod archive, string apikey)
        {
            var client = BaseNexusClient(apikey);
            string url;
            string get_url_link = String.Format("https://api.nexusmods.com/v1/games/{0}/mods/{1}/files/{2}/download_link.json",
                                                ConvertGameName(archive.GameName), archive.ModID, archive.FileID);
            using (var s = client.GetStreamSync(get_url_link))
            {
                url = s.FromJSON<List<DownloadLink>>().First().URI;
                return url;
            }
        }

        private static string ConvertGameName(string gameName)
        {
            if (gameName == "SkyrimSE") return "skyrimspecialedition";
            return gameName;

        }

    }
}
