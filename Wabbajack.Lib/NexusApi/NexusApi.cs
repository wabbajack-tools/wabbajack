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
using Syroot.Windows.IO;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using WebSocketSharp;
using static Wabbajack.Lib.NexusApi.NexusApiUtils;

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
                    _userStatus = GetUserStatus().Result;
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

                var cacheFolder = Path.Combine(new KnownFolder(KnownFolderType.LocalAppData).Path, "Wabbajack");
                if (!Directory.Exists(cacheFolder))
                {
                    Directory.CreateDirectory(cacheFolder);
                }

                var cacheFile = Path.Combine(cacheFolder, _additionalEntropy);
                if (File.Exists(cacheFile))
                {
                    try
                    {
                        return Encoding.UTF8.GetString(
                            ProtectedData.Unprotect(File.ReadAllBytes(cacheFile),
                            Encoding.UTF8.GetBytes(_additionalEntropy), DataProtectionScope.CurrentUser));
                    }
                    catch (CryptographicException)
                    {
                        File.Delete(cacheFile);
                    }
                }

                var env_key = Environment.GetEnvironmentVariable("NEXUSAPIKEY");
                if (env_key != null)
                {
                    return env_key;
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
                File.WriteAllBytes(cacheFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(result), 
                    Encoding.UTF8.GetBytes(_additionalEntropy), DataProtectionScope.CurrentUser));

                return result;
            }
        }

        public async Task<UserStatus> GetUserStatus()
        {
            var url = "https://api.nexusmods.com/v1/users/validate.json";
            return await Get<UserStatus>(url);
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

        private async Task<T> Get<T>(string url)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            UpdateRemaining(response);

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                return stream.FromJSON<T>();
            }
        }

        private async Task<T> GetCached<T>(string url)
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

                var result = await Get<T>(url);
                if (result != null)
                    result.ToJSON(cache_file);
                return result;
            }

            try
            {
                return await Get<T>(Consts.WabbajackCacheLocation + code);
            }
            catch (Exception)
            {
                return await Get<T>(url);
            }

        }

        public async Task<string> GetNexusDownloadLink(NexusDownloader.State archive, bool cache = false)
        {
            if (cache)
            {
                var result = await TryGetCachedLink(archive);
                if (result.Succeeded)
                {
                    return result.Value;
                }
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(archive.GameName)}/mods/{archive.ModID}/files/{archive.FileID}/download_link.json";
            return (await Get<List<DownloadLink>>(url)).First().URI;
        }

        private async Task<GetResponse<string>> TryGetCachedLink(NexusDownloader.State archive)
        {
            if (!Directory.Exists(Consts.NexusCacheDirectory))
                Directory.CreateDirectory(Consts.NexusCacheDirectory);

            var path = Path.Combine(Consts.NexusCacheDirectory, $"link-{archive.GameName}-{archive.ModID}-{archive.FileID}.txt");
            if (!File.Exists(path) || (DateTime.Now - new FileInfo(path).LastWriteTime).TotalHours > 24)
            {
                File.Delete(path);
                var result = await GetNexusDownloadLink(archive);
                File.WriteAllText(path, result);
                return GetResponse<string>.Succeed(result);
            }

            return GetResponse<string>.Succeed(File.ReadAllText(path));
        }

        public async Task<NexusFileInfo> GetFileInfo(NexusDownloader.State mod)
        {
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/files/{mod.FileID}.json";
            return await GetCached<NexusFileInfo>(url);
        }

        public class GetModFilesResponse
        {
            public List<NexusFileInfo> files;
        }

        public async Task<GetModFilesResponse> GetModFiles(Game game, int modid)
        {
            var url = $"https://api.nexusmods.com/v1/games/{GameRegistry.Games[game].NexusName}/mods/{modid}/files.json";
            return await GetCached<GetModFilesResponse>(url);
        }

        public async Task<List<MD5Response>> GetModInfoFromMD5(Game game, string md5Hash)
        {
            var url = $"https://api.nexusmods.com/v1/games/{GameRegistry.Games[game].NexusName}/mods/md5_search/{md5Hash}.json";
            return await Get<List<MD5Response>>(url);
        }

        public async Task<ModInfo> GetModInfo(Game game, string modId)
        {
            var url = $"https://api.nexusmods.com/v1/games/{game.MetaData().NexusName}/mods/{modId}.json";
            return await GetCached<ModInfo>(url);
        }

        public async Task<EndorsementResponse> EndorseMod(NexusDownloader.State mod)
        {
            Utils.Status($"Endorsing ${mod.GameName} - ${mod.ModID}");
            var url = $"https://api.nexusmods.com/v1/games/{ConvertGameName(mod.GameName)}/mods/{mod.ModID}/endorse.json";

            var content = new FormUrlEncodedContent(new Dictionary<string, string> { { "version", mod.Version } });

            using (var stream = await _httpClient.PostStream(url, content))
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

        public async Task ClearUpdatedModsInCache()
        {
            if (!UseLocalCache) return;

            var gameTasks = GameRegistry.Games.Values
                .Where(game => game.NexusName != null)
                .Select(async game =>
                {
                    return (game,
                        mods: await Get<List<UpdatedMod>>(
                            $"https://api.nexusmods.com/v1/games/{game.NexusName}/mods/updated.json?period=1m"));
                })
                .Select(async rTask =>
                {
                    var (game, mods) = await rTask;
                    return mods.Select(mod => new { game = game, mod = mod });
                });
            var purge = (await Task.WhenAll(gameTasks))
                .SelectMany(i => i)
                .ToList();

            Utils.Log($"Found {purge.Count} updated mods in the last month");
            using (var queue = new WorkQueue())
            {
                var to_purge = (await Directory.EnumerateFiles(LocalCacheDir, "*.json")
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

                        // ToDo
                        // Can improve to not read the entire file to see if it starts with null
                        if (File.ReadAllText(f).StartsWith("null"))
                            return (true, f);

                        return (false, f);
                    }))
                    .Where(p => p.Item1)
                    .ToList();

                Utils.Log($"Purging {to_purge.Count} cache entries");
                await to_purge.PMap(queue, f =>
                {
                    var uri = new Uri(Encoding.UTF8.GetString(Path.GetFileNameWithoutExtension(f.f).FromHex()));
                    Utils.Log($"Purging {uri}");
                    File.Delete(f.f);
                });
            }

        }
    }

}
