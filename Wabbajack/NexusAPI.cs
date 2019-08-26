using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
            _websocket.Send("{\"id\": \"" + guid + "\", \"appid\": \""+ Consts.AppName+"\"}");

            Process.Start($"https://www.nexusmods.com/sso?id={guid}&application=" + Consts.AppName);

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
            _baseHttpClient.DefaultRequestHeaders.Add("Application-Name", Consts.AppName);
            _baseHttpClient.DefaultRequestHeaders.Add("Application-Version", $"{Assembly.GetEntryAssembly().GetName().Version}");
            return _baseHttpClient;
        }

        public static string GetNexusDownloadLink(NexusMod archive, string apikey, bool cache=true)
        {
            if (cache && TryGetCachedLink(archive, apikey, out string result)) return result;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
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

        private static bool TryGetCachedLink(NexusMod archive, string apikey, out string result)
        {
            if (!(Directory.Exists(Consts.NexusCacheDirectory)))
                Directory.CreateDirectory(Consts.NexusCacheDirectory);


            string path = Path.Combine(Consts.NexusCacheDirectory, $"link-{archive.GameName}-{archive.ModID}-{archive.FileID}.txt");
            if (!File.Exists(path) || DateTime.Now - new FileInfo(path).LastWriteTime > new TimeSpan(24, 0, 0))
            {
                File.Delete(path);
                result = GetNexusDownloadLink(archive, apikey, false);
                File.WriteAllText(path, result);
                return true;
            }
            result = File.ReadAllText(path);
            return true;
        }

        private static string ConvertGameName(string gameName)
        {
            if (gameName == "SkyrimSE") return "skyrimspecialedition";
            if (gameName == "FalloutNV") return "newvegas";
            return gameName;

        }


        public class UserStatus
        {
            public string user_id;
            public string key;
            public string name;
            public bool is_premium;
            public bool is_supporter;
            public string email;
            public string profile_url;
        }

        
        public static UserStatus GetUserStatus(string apikey)
        {
            var url = "https://api.nexusmods.com/v1/users/validate.json";
            var client = BaseNexusClient(apikey);

            using (var s = client.GetStreamSync(url))
            {
                return s.FromJSON<UserStatus>();
            }
        }

        public class NexusFileInfo
        {
            public ulong file_id;
            public string name;
            public string version;
            public ulong category_id;
            public string category_name;
            public bool is_primary;
            public ulong size;
            public string file_name;
            public ulong uploaded_timestamp;
            public DateTime uploaded_time;
            public string mod_version;
            public string external_virus_scan_url;
            public string description;
            public ulong size_kb;
            public string changelog_html;
        }


        public static NexusFileInfo GetFileInfo(NexusMod mod, string apikey)
        {
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/files/{mod.FileID}.json";
            var client = BaseNexusClient(apikey);

            using (var s = client.GetStreamSync(url))
            {
                return s.FromJSON<NexusFileInfo>();
            }
        }

        public static EndorsementResponse EndorseMod(NexusMod mod, string apikey)
        {
            Utils.Status($"Endorsing ${mod.GameName} - ${mod.ModID}");
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/endorse.json";
            var client = BaseNexusClient(apikey);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>() {{"value", "\"\""}});

            using (var s = client.PostStreamSync(url, content))
            {
                return s.FromJSON<EndorsementResponse>();
            }
        }

    }

    public class EndorsementResponse
    {
        public string message;
        public string status;
    }
}
