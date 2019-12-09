using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Syroot.Windows.IO;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.LibCefHelpers;
using WebSocketSharp;
using Xilium.CefGlue;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Common.Handlers;
using Xilium.CefGlue.WPF;
using static Wabbajack.Lib.NexusApi.NexusApiUtils;
using System.Threading;

namespace Wabbajack.Lib.NexusApi
{
    public class NexusApiClient : ViewModel
    {
        private static readonly string API_KEY_CACHE_FILE = "nexus.key_cache";
        private static string _additionalEntropy = "vtP2HF6ezg";

        private readonly HttpClient _httpClient;

        public HttpClient HttpClient => _httpClient;


        #region Authentication

        private readonly string _apiKey;

        public string ApiKey => _apiKey;

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
                // Clean up old location
                if (File.Exists(API_KEY_CACHE_FILE))
                {
                    File.Delete(API_KEY_CACHE_FILE);
                }

                try
                {
                    return Utils.FromEncryptedJson<string>("nexusapikey");
                }
                catch (Exception)
                {
                }

                var env_key = Environment.GetEnvironmentVariable("NEXUSAPIKEY");
                if (env_key != null)
                {
                    return env_key;
                }

                var result = Utils.Log(new RequestNexusAuthorization()).Task.Result;
                result.ToEcryptedJson("nexusapikey");
                return result;
            }
        }

        class RefererHandler : RequestHandler
        {
            private string _referer;

            public RefererHandler(string referer)
            {
                _referer = referer;
            }
            protected override bool OnBeforeBrowse(CefBrowser browser, CefFrame frame, CefRequest request, bool userGesture, bool isRedirect)
            {
                base.OnBeforeBrowse(browser, frame, request, userGesture, isRedirect);
                if (request.ReferrerURL == null)
                    request.SetReferrer(_referer, CefReferrerPolicy.Default);
                return false;
            }
        }

        public static async Task<string> SetupNexusLogin(BaseCefBrowser browser, Action<string> updateStatus, CancellationToken cancel)
        {
            updateStatus("Please Log Into the Nexus");
            browser.Address = "https://users.nexusmods.com/auth/continue?client_id=nexus&redirect_uri=https://www.nexusmods.com/oauth/callback&response_type=code&referrer=//www.nexusmods.com";
            while (true)
            {
                var cookies = await Helpers.GetCookies("nexusmods.com");
                if (cookies.Any(c => c.Name == "member_id"))
                    break;
                cancel.ThrowIfCancellationRequested();
                await Task.Delay(500, cancel);
            }

            // open a web socket to receive the api key
            var guid = Guid.NewGuid();
            using (var websocket = new WebSocket("wss://sso.nexusmods.com")
            {
                SslConfiguration =
                {
                    EnabledSslProtocols = SslProtocols.Tls12
                }
            })
            {
                updateStatus("Please Authorize Wabbajack to Download Mods");
                var api_key = new TaskCompletionSource<string>();
                websocket.OnMessage += (sender, msg) => { api_key.SetResult(msg.Data); };

                websocket.Connect();
                websocket.Send("{\"id\": \"" + guid + "\", \"appid\": \"" + Consts.AppName + "\"}");
                await Task.Delay(1000, cancel);

                // open a web browser to get user permission
                browser.Address = $"https://www.nexusmods.com/sso?id={guid}&application={Consts.AppName}";
                using (cancel.Register(() =>
                {
                    api_key.SetCanceled();
                }))
                {
                    return await api_key.Task;
                }
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
            try
            {
                var dailyRemaining = int.Parse(response.Headers.GetValues("x-rl-daily-remaining").First());
                var hourlyRemaining = int.Parse(response.Headers.GetValues("x-rl-hourly-remaining").First());

                lock (RemainingLock)
                {
                    _dailyRemaining = Math.Min(dailyRemaining, hourlyRemaining);
                    _hourlyRemaining = Math.Min(dailyRemaining, hourlyRemaining);
                }
                this.RaisePropertyChanged(nameof(DailyRemaining));
                this.RaisePropertyChanged(nameof(HourlyRemaining));
            }
            catch (Exception)
            {
            }

        }

        #endregion


        public NexusApiClient(string apiKey = null)
        {
            _apiKey = apiKey ?? GetApiKey();
            _httpClient = new HttpClient();

            // set default headers for all requests to the Nexus API
            var headers = _httpClient.DefaultRequestHeaders;
            headers.Add("User-Agent", Consts.UserAgent);
            headers.Add("apikey", _apiKey);
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            headers.Add("Application-Name", Consts.AppName);
            headers.Add("Application-Version", $"{Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 1)}");

            if (!Directory.Exists(Consts.NexusCacheDirectory))
                Directory.CreateDirectory(Consts.NexusCacheDirectory);
        }

        private T Get<T>(string url)
        {
            Task<HttpResponseMessage> responseTask = _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            responseTask.Wait();

            var response = responseTask.Result;
            UpdateRemaining(response);

            var contentTask = response.Content.ReadAsStreamAsync();
            contentTask.Wait();

            using (var stream = contentTask.Result)
            {
                return stream.FromJSON<T>();
            }
        }

        private T GetCached<T>(string url)
        {
            var code = Encoding.UTF8.GetBytes(url).ToHex() + ".json";

            if (UseLocalCache)
            {
                if (!Directory.Exists(LocalCacheDir))
                    Directory.CreateDirectory(LocalCacheDir);

                var cache_file = Path.Combine(LocalCacheDir, code);
                if (File.Exists(cache_file))
                {
                    return cache_file.FromJSON<T>();
                }

                var result = Get<T>(url);
                if (result != null)
                    result.ToJSON(cache_file);
                return result;
            }

            try
            {
                return Get<T>(Consts.WabbajackCacheLocation + code);
            }
            catch (Exception)
            {
                return Get<T>(url);
            }

        }

        public string GetNexusDownloadLink(NexusDownloader.State archive, bool cache = false)
        {
            if (cache && TryGetCachedLink(archive, out var result))
                return result;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(archive.GameName)}/mods/{archive.ModID}/files/{archive.FileID}/download_link.json";
            return Get<List<DownloadLink>>(url).First().URI;
        }

        private bool TryGetCachedLink(NexusDownloader.State archive, out string result)
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

        public NexusFileInfo GetFileInfo(NexusDownloader.State mod)
        {
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/files/{mod.FileID}.json";
            return GetCached<NexusFileInfo>(url);
        }

        public class GetModFilesResponse
        {
            public List<NexusFileInfo> files;
        }

        public GetModFilesResponse GetModFiles(Game game, int modid)
        {
            var url = $"https://api.nexusmods.com/v1/games/{game.MetaData().NexusName}/mods/{modid}/files.json";
            return GetCached<GetModFilesResponse>(url);
        }

        public List<MD5Response> GetModInfoFromMD5(Game game, string md5Hash)
        {
            var url = $"https://api.nexusmods.com/v1/games/{game.MetaData().NexusName}/mods/md5_search/{md5Hash}.json";
            return Get<List<MD5Response>>(url);
        }

        public ModInfo GetModInfo(Game game, string modId)
        {
            var url = $"https://api.nexusmods.com/v1/games/{game.MetaData().NexusName}/mods/{modId}.json";
            return GetCached<ModInfo>(url);
        }

        public EndorsementResponse EndorseMod(NexusDownloader.State mod)
        {
            Utils.Status($"Endorsing ${mod.GameName} - ${mod.ModID}");
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/endorse.json";

            var content = new FormUrlEncodedContent(new Dictionary<string, string> { { "version", mod.Version } });

            using (var stream = _httpClient.PostStreamSync(url, content))
            {
                return stream.FromJSON<EndorsementResponse>();
            }
        }

        private class DownloadLink
        {
            public string URI { get; set; }
        }

        private class UpdatedMod
        {
            public long mod_id;
            public long latest_file_update;
            public long latest_mod_activity;
        }

        private static bool? _useLocalCache;
        public static bool UseLocalCache
        {
            get
            {
                if (_useLocalCache == null) return LocalCacheDir != null;
                return _useLocalCache ?? false;
            }
            set => _useLocalCache = value;
        }

        private static string _localCacheDir;
        public static string LocalCacheDir
        {
            get
            {
                if (_localCacheDir == null)
                    _localCacheDir = Environment.GetEnvironmentVariable("NEXUSCACHEDIR");
                return _localCacheDir;
            }
            set => _localCacheDir = value;
        }

        public void ClearUpdatedModsInCache()
        {
            if (!UseLocalCache) return;

            var purge = GameRegistry.Games.Values
                .Where(game => game.NexusName != null)
                .Select(game => new
                {
                    game = game,
                    mods = Get<List<UpdatedMod>>(
                        $"https://api.nexusmods.com/v1/games/{game.NexusName}/mods/updated.json?period=1m")
                })
                .SelectMany(r => r.mods.Select(mod => new {game = r.game, 
                                                           mod = mod}))
                .ToList();

            Utils.Log($"Found {purge.Count} updated mods in the last month");
            using (var queue = new WorkQueue())
            {
                var to_purge = Directory.EnumerateFiles(LocalCacheDir, "*.json")
                    .PMap(queue,f =>
                    {
                        Utils.Status("Cleaning Nexus cache for");
                        var uri = new Uri(Encoding.UTF8.GetString(Path.GetFileNameWithoutExtension(f).FromHex()));
                        var parts = uri.PathAndQuery.Split('/', '.').ToHashSet();
                        var found = purge.FirstOrDefault(p =>
                            parts.Contains(p.game.NexusName) && parts.Contains(p.mod.mod_id.ToString()));
                        if (found != null)
                        {
                            var should_remove =
                                File.GetLastWriteTimeUtc(f) <= found.mod.latest_file_update.AsUnixTime();
                            return (should_remove, f);
                        }

                        if (File.ReadAllText(f).StartsWith("null"))
                            return (true, f);

                        return (false, f);
                    })
                    .Where(p => p.Item1)
                    .ToList();

                Utils.Log($"Purging {to_purge.Count} cache entries");
                to_purge.PMap(queue, f =>
                {
                    var uri = new Uri(Encoding.UTF8.GetString(Path.GetFileNameWithoutExtension(f.f).FromHex()));
                    Utils.Log($"Purging {uri}");
                    File.Delete(f.f);
                });
            }

        }
    }

}
