using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using System.Threading;
using Wabbajack.Common.Exceptions;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.NexusApi
{
    public class NexusApiClient : ViewModel
    {
        private static readonly string API_KEY_CACHE_FILE = "nexus.key_cache";
       
        public Common.Http.Client HttpClient { get; } = new Common.Http.Client();

        #region Authentication

        public string? ApiKey { get; }

        public bool IsAuthenticated => ApiKey != null;

        private Task<UserStatus>? _userStatus;
        public Task<UserStatus> UserStatus
        {
            get
            {
                if (_userStatus == null)
                    _userStatus = GetUserStatus();
                return _userStatus;
            }
        }

        public async Task<bool> IsPremium()
        {
            return IsAuthenticated && (await UserStatus).is_premium;
        }

        public async Task<string> Username() => (await UserStatus).name;

        private static AsyncLock _getAPIKeyLock = new AsyncLock();
        private static async Task<string> GetApiKey()
        {
            using (await _getAPIKeyLock.WaitAsync())
            {
                // Clean up old location
                if (File.Exists(API_KEY_CACHE_FILE))
                {
                    File.Delete(API_KEY_CACHE_FILE);
                }

                try
                {
                    return await Utils.FromEncryptedJson<string>("nexusapikey");
                }
                catch (Exception)
                {
                }

                var env_key = Environment.GetEnvironmentVariable("NEXUSAPIKEY");
                if (env_key != null)
                {
                    return env_key;
                }

                return await RequestAndCacheAPIKey();
            }
        }

        public static async Task<string> RequestAndCacheAPIKey()
        {
            var result = await Utils.Log(new RequestNexusAuthorization()).Task;
            await result.ToEcryptedJson("nexusapikey");
            return result;
        }

        public static async Task<string> SetupNexusLogin(IWebDriver browser, Action<string> updateStatus, CancellationToken cancel)
        {
            updateStatus("Please log into the Nexus");
            await browser.NavigateTo(new Uri("https://users.nexusmods.com/auth/continue?client_id=nexus&redirect_uri=https://www.nexusmods.com/oauth/callback&response_type=code&referrer=//www.nexusmods.com"));
            while (true)
            {
                var cookies = await browser.GetCookies("nexusmods.com");
                if (cookies.Any(c => c.Name == "member_id"))
                    break;
                cancel.ThrowIfCancellationRequested();
                await Task.Delay(500, cancel);
            }


            await browser.NavigateTo(new Uri("https://www.nexusmods.com/users/myaccount?tab=api"));

            updateStatus("Looking for API Key");

            
            var apiKey = new TaskCompletionSource<string>();

            while (true)
            {
                var key = "";
                try
                {
                    key = await browser.EvaluateJavaScript(
                        "document.querySelector(\"input[value=wabbajack]\").parentElement.parentElement.querySelector(\"textarea.application-key\").innerHTML");
                }
                catch (Exception)
                {
                    // ignored
                }

                if (!string.IsNullOrEmpty(key))
                {
                    return key;
                }

                try
                {
                    await browser.EvaluateJavaScript(
                        "var found = document.querySelector(\"input[value=wabbajack]\").parentElement.parentElement.querySelector(\"form button[type=submit]\");" +
                        "found.onclick= function() {return true;};" +
                        "found.class = \" \"; " +
                        "found.click();" +
                        "found.remove(); found = undefined;"
                    );
                    updateStatus("Generating API Key, Please Wait...");


                }
                catch (Exception)
                {
                    // ignored
                }

                cancel.ThrowIfCancellationRequested();
                await Task.Delay(500);
            }
        }

        public async Task<UserStatus> GetUserStatus()
        {
            var url = "https://api.nexusmods.com/v1/users/validate.json";
            return await Get<UserStatus>(url);
        }

        public async Task<(int, int)> GetRemainingApiCalls()
        {
            var url = "https://api.nexusmods.com/v1/users/validate.json";
            using var response = await HttpClient.GetAsync(url);
            var result =  (int.Parse(response.Headers.GetValues("X-RL-Daily-Remaining").First()),
                int.Parse(response.Headers.GetValues("X-RL-Hourly-Remaining").First()));
            _dailyRemaining = result.Item1;
            _hourlyRemaining = result.Item2;
            return result;
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
            protected set
            {
                lock (RemainingLock)
                {
                    _dailyRemaining = value;
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
            protected set
            {
                lock (RemainingLock)
                {
                    _hourlyRemaining = value;
                }
            }

        }


        protected virtual async Task UpdateRemaining(HttpResponseMessage response)
        {
            try
            {
                var oldDaily = _dailyRemaining;
                var oldHourly = _hourlyRemaining;
                var dailyRemaining = int.Parse(response.Headers.GetValues("x-rl-daily-remaining").First());
                var hourlyRemaining = int.Parse(response.Headers.GetValues("x-rl-hourly-remaining").First());

                lock (RemainingLock)
                {
                    _dailyRemaining = Math.Min(_dailyRemaining, dailyRemaining);
                    _hourlyRemaining = Math.Min(_hourlyRemaining, hourlyRemaining);
                }
                
                if (oldDaily != _dailyRemaining || oldHourly != _hourlyRemaining) 
                    Utils.Log($"Nexus requests remaining: {_dailyRemaining} daily - {_hourlyRemaining} hourly");

                this.RaisePropertyChanged(nameof(DailyRemaining));
                this.RaisePropertyChanged(nameof(HourlyRemaining));
            }
            catch (Exception)
            {
            }

        }

        #endregion

        protected NexusApiClient(string? apiKey = null)
        {
            ApiKey = apiKey;

            // set default headers for all requests to the Nexus API
            var headers = HttpClient.Headers;
            headers.Add(("User-Agent", Consts.UserAgent));
            headers.Add(("apikey", ApiKey));
            headers.Add(("Accept", "application/json"));
            headers.Add(("Application-Name", Consts.AppName));
            headers.Add(("Application-Version", $"{Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 1)}"));

            if (!Directory.Exists(Consts.NexusCacheDirectory))
                Directory.CreateDirectory(Consts.NexusCacheDirectory);
        }

        public static async Task<NexusApiClient> Get(string? apiKey = null)
        {
            apiKey ??= await GetApiKey();
            return new NexusApiClient(apiKey);
        }

        public async Task<T> Get<T>(string url)
        {
            int retries = 0;
            TOP:
            try
            {
                using var response = await HttpClient.GetAsync(url);
                await UpdateRemaining(response);
                if (!response.IsSuccessStatusCode)
                {
                    Utils.Log($"Nexus call failed: {response.RequestMessage.RequestUri}");
                    throw new HttpException(response);
                }


                await using var stream = await response.Content.ReadAsStreamAsync();
                return stream.FromJson<T>(genericReader:true);
            }
            catch (TimeoutException)
            {
                if (retries == Consts.MaxHTTPRetries)
                    throw;
                Utils.Log($"Nexus call to {url} failed, retrying {retries} of {Consts.MaxHTTPRetries}");
                retries++;
                goto TOP;
            }
            catch (Exception e)
            {
                Utils.Log(e.ToString());
                throw;
            }
        }

        private async Task<T> GetCached<T>(string url)
        {
            try
            {
                var builder = new UriBuilder(url)
                {
                    Host = Consts.WabbajackBuildServerUri.Host, 
                    Scheme = Consts.WabbajackBuildServerUri.Scheme, 
                    Port = Consts.WabbajackBuildServerUri.Port
                };
                return await Get<T>(builder.ToString());
            }
            catch (Exception)
            {
                return await Get<T>(url);
            }

        }

        public async Task<string> GetNexusDownloadLink(NexusDownloader.State archive)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var url = $"https://api.nexusmods.com/v1/games/{archive.Game.MetaData().NexusName}/mods/{archive.ModID}/files/{archive.FileID}/download_link.json";
            try
            {
                return (await Get<List<DownloadLink>>(url)).First().URI;
            }
            catch (HttpException ex)
            {
                if (ex.Code != 403 || await IsPremium())
                {
                    throw;
                }
            }

            try
            {
                Utils.Log($"Requesting manual download for {archive.Name}");
                return (await Utils.Log(await ManuallyDownloadNexusFile.Create(archive)).Task).ToString();
            }
            catch (TaskCanceledException ex)
            {
                Utils.Error(ex, "Manual cancellation of download");
                throw;
            }
        }

        public class GetModFilesResponse
        {
            public List<NexusFileInfo> files { get; set; } = new List<NexusFileInfo>();
        }

        public async Task<GetModFilesResponse> GetModFiles(Game game, long modid, bool useCache = true)
        
        {
            var url = $"https://api.nexusmods.com/v1/games/{game.MetaData().NexusName}/mods/{modid}/files.json";
            var result = useCache ? await GetCached<GetModFilesResponse>(url) : await Get<GetModFilesResponse>(url);
            if (result.files == null)
                throw new InvalidOperationException("Got Null data from the Nexus while finding mod files");
            return result;
        }

        public async Task<List<MD5Response>> GetModInfoFromMD5(Game game, string md5Hash)
        {
            var url = $"https://api.nexusmods.com/v1/games/{game.MetaData().NexusName}/mods/md5_search/{md5Hash}.json";
            return await Get<List<MD5Response>>(url);
        }

        public async Task<ModInfo> GetModInfo(Game game, long modId, bool useCache = true)
        {
            var url = $"https://api.nexusmods.com/v1/games/{game.MetaData().NexusName}/mods/{modId}.json";
            if (useCache)
            {
                return await GetCached<ModInfo>(url);
            }

            return await Get<ModInfo>(url);
        }

        private class DownloadLink
        {
            public string URI { get; set; } = string.Empty;
        }

        private static string? _localCacheDir;
        public static string LocalCacheDir
        {
            get
            {
                if (_localCacheDir == null)
                    _localCacheDir = Environment.GetEnvironmentVariable("NEXUSCACHEDIR");
                if (_localCacheDir == null)
                    throw new ArgumentNullException($"Enviornment variable could not be located: NEXUSCACHEDIR");
                return _localCacheDir;
            }
            set => _localCacheDir = value;
        }

        public static Uri ManualDownloadUrl(NexusDownloader.State state)
        {
            return new Uri($"https://www.nexusmods.com/{state.Game.MetaData().NexusName}/mods/{state.ModID}?tab=files");
        }
    }
}
