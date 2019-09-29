using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Authentication;
using System.Threading.Tasks;
using Wabbajack.Common;
using static Wabbajack.NexusApi.NexusApiUtils;
using WebSocketSharp;

namespace Wabbajack.NexusApi
{
    public class NexusApiClient : INotifyPropertyChanged
    {
        private static readonly string API_KEY_CACHE_FILE = "nexus.key_cache";

        private static readonly uint CACHED_VERSION_NUMBER = 1;

        
        private readonly HttpClient _httpClient;


        #region Authentication

        private readonly string _apiKey;

        public bool IsAuthenticated => _apiKey != null;

        private UserStatus _userStatus;

        public UserStatus UserStatus
        {
            get
            {
                if (_userStatus == null)
                    _userStatus = GetUserStatus();
                return _userStatus;
            }
        }

        public bool IsPremium => IsAuthenticated && UserStatus.is_premium;

        public string Username => UserStatus?.name;


        private static object _getAPIKeyLock = new object();
        private static string GetApiKey()
        {
            lock (_getAPIKeyLock)
            {
                // check if there exists a cached api key
                var fi = new FileInfo(API_KEY_CACHE_FILE);
                if (fi.Exists && fi.LastWriteTime > DateTime.Now.AddHours(-72))
                {
                    return File.ReadAllText(API_KEY_CACHE_FILE);
                }

                // open a web socket to receive the api key
                var guid = Guid.NewGuid();
                var _websocket = new WebSocket("wss://sso.nexusmods.com")
                {
                    SslConfiguration =
                    {
                        EnabledSslProtocols = SslProtocols.Tls12
                    }
                };

                var api_key = new TaskCompletionSource<string>();
                _websocket.OnMessage += (sender, msg) => { api_key.SetResult(msg.Data); };

                _websocket.Connect();
                _websocket.Send("{\"id\": \"" + guid + "\", \"appid\": \"" + Consts.AppName + "\"}");

                // open a web browser to get user permission
                Process.Start($"https://www.nexusmods.com/sso?id={guid}&application=" + Consts.AppName);

                // get the api key from the socket and cache it
                api_key.Task.Wait();
                var result = api_key.Task.Result;
                File.WriteAllText(API_KEY_CACHE_FILE, result);

                return result;
            }
        }

        public UserStatus GetUserStatus()
        {
            var url = "https://api.nexusmods.com/v1/users/validate.json";
            return Get<UserStatus>(url);
        }

        #endregion

        #region Rate Tracking

        private readonly object RemainingLock = new object();

        private int _dailyRemaining;
        public int DailyRemaining
        {
            get
            {
                lock (RemainingLock)
                {
                    return _dailyRemaining;
                }
            }
        }

        private int _hourlyRemaining;
        public int HourlyRemaining
        {
            get
            {
                lock (RemainingLock)
                {
                    return _hourlyRemaining;
                }
            }
        }


        private void UpdateRemaining(HttpResponseMessage response)
        {
            int dailyRemaining = int.Parse(response.Headers.GetValues("x-rl-daily-remaining").First());
            int hourlyRemaining = int.Parse(response.Headers.GetValues("x-rl-hourly-remaining").First());

            lock (RemainingLock)
            {
                _dailyRemaining = Math.Min(dailyRemaining, hourlyRemaining);
                _hourlyRemaining = Math.Min(dailyRemaining, hourlyRemaining);
            }
            OnPropertyChanged(nameof(DailyRemaining));
            OnPropertyChanged(nameof(HourlyRemaining));

        }

        #endregion


        public NexusApiClient()
        {
            _apiKey = GetApiKey();
            _httpClient = new HttpClient();

            // set default headers for all requests to the Nexus API
            var headers = _httpClient.DefaultRequestHeaders;
            headers.Add("User-Agent", Consts.UserAgent);
            headers.Add("apikey", _apiKey);
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            headers.Add("Application-Name", Consts.AppName);
            headers.Add("Application-Version", $"{Assembly.GetEntryAssembly().GetName().Version}");
        }
        
        private T Get<T>(string url)
        {
            Task<HttpResponseMessage> responseTask = _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            responseTask.Wait();

            var response = responseTask.Result;
            UpdateRemaining(response);

            using (var stream = _httpClient.GetStreamSync(url))
            {
                return stream.FromJSON<T>();
            }
        }
        

        public string GetNexusDownloadLink(NexusMod archive, bool cache = false)
        {
            if (cache && TryGetCachedLink(archive, out var result))
                return result;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(archive.GameName)}/mods/{archive.ModID}/files/{archive.FileID}/download_link.json";
            return Get<List<DownloadLink>>(url).First().URI;
        }

        private bool TryGetCachedLink(NexusMod archive, out string result)
        {
            if (!Directory.Exists(Consts.NexusCacheDirectory))
                Directory.CreateDirectory(Consts.NexusCacheDirectory);

            var path = Path.Combine(Consts.NexusCacheDirectory, $"link-{archive.GameName}-{archive.ModID}-{archive.FileID}.txt");
            if (!File.Exists(path) || (DateTime.Now - new FileInfo(path).LastWriteTime).TotalHours > 24)
            {
                File.Delete(path);
                result = GetNexusDownloadLink(archive);
                File.WriteAllText(path, result);
                return true;
            }

            result = File.ReadAllText(path);
            return true;
        }

        public NexusFileInfo GetFileInfo(NexusMod mod)
        {
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/files/{mod.FileID}.json";
            return Get<NexusFileInfo>(url);
        }

        public ModInfo GetModInfo(NexusMod archive)
        {
            if (!Directory.Exists(Consts.NexusCacheDirectory))
                Directory.CreateDirectory(Consts.NexusCacheDirectory);

            ModInfo result = null;
            TOP:
            var path = Path.Combine(Consts.NexusCacheDirectory, $"mod-info-{archive.GameName}-{archive.ModID}.json");
            try
            {
                if (File.Exists(path))
                {
                    result = path.FromJSON<ModInfo>();
                    if (result._internal_version != CACHED_VERSION_NUMBER)
                    {
                        File.Delete(path);
                        goto TOP;
                    }

                    return result;
                }
            }
            catch (Exception)
            {
                File.Delete(path);
            }

            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(archive.GameName)}/mods/{archive.ModID}.json";
            result = Get<ModInfo>(url);

            result.game_name = archive.GameName;
            result.mod_id = archive.ModID;
            result._internal_version = CACHED_VERSION_NUMBER;
            result.ToJSON(path);
            return result;
        }

        public EndorsementResponse EndorseMod(NexusMod mod)
        {
            Utils.Status($"Endorsing ${mod.GameName} - ${mod.ModID}");
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/endorse.json";

            var content = new FormUrlEncodedContent(new Dictionary<string, string> {{"version", mod.Version}});

            using (var stream = _httpClient.PostStreamSync(url, content))
            {
                return stream.FromJSON<EndorsementResponse>();
            }
        }


        public static IEnumerable<SlideShowItem> CachedSlideShow
        {
            get
            {
                if (!Directory.Exists(Consts.NexusCacheDirectory)) return new SlideShowItem[]{};

                return Directory.EnumerateFiles(Consts.NexusCacheDirectory)
                    .Where(f => f.EndsWith(".json"))
                    .Select(f => f.FromJSON<ModInfo>())
                    .Where(m => m._internal_version == CACHED_VERSION_NUMBER && m.picture_url != null)
                    .Select(m => new SlideShowItem
                    {
                        ImageURL =  m.picture_url,
                        ModName = FixupSummary(m.name),
                        AuthorName = FixupSummary(m.author),
                        ModURL = GetModURL(m.game_name, m.mod_id),
                        ModSummary = FixupSummary(m.summary)
                    });
            }
        }


        private class DownloadLink
        {
            public string URI { get; set; }
        }


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }

}