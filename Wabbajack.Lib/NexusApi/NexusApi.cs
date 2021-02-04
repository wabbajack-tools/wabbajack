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
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.NexusApi
{
    public class NexusApiClient : ViewModel, INexusApi
    {
        private static readonly string API_KEY_CACHE_FILE = "nexus.key_cache";
        /// <summary>
        /// Forces the client to do manual downloading via CEF (for testing)
        /// </summary>
        private static bool ManualTestingMode = false;
       
        public Http.Client HttpClient { get; } = new();

        #region Authentication

        public static string? ApiKey { get; set; }

        public bool IsAuthenticated => ApiKey != null;
        public int RemainingAPICalls => Math.Max(HourlyRemaining, DailyRemaining);

        private Task<UserStatus>? _userStatus;
        public Task<UserStatus> UserStatus
        {
            get
            {
                return _userStatus ??= GetUserStatus();
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

            Helpers.Cookie[] cookies = {};
            while (true)
            {
                cookies = await browser.GetCookies("nexusmods.com");
                if (cookies.Any(c => c.Name == "member_id"))
                    break;
                cancel.ThrowIfCancellationRequested();
                await Task.Delay(500, cancel);
            }


            await browser.NavigateTo(new Uri("https://www.nexusmods.com/users/myaccount?tab=api"));

            updateStatus("Saving login info");

            await cookies.ToEcryptedJson("nexus-cookies");
            
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
            var result = await Get<UserStatus>(url);

            Utils.Log($"Logged into the nexus as {result.name}");
            Utils.Log($"Nexus calls remaining: {DailyRemaining} daily, {HourlyRemaining} hourly");

            return result;
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
                _dailyRemaining = int.Parse(response.Headers.GetValues("x-rl-daily-remaining").First());
                _hourlyRemaining = int.Parse(response.Headers.GetValues("x-rl-hourly-remaining").First());

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
        }

        public static async Task<NexusApiClient> Get(string? apiKey = null)
        {
            apiKey ??= await GetApiKey();
            return new NexusApiClient(apiKey);
        }

        public async Task<T> Get<T>(string url, Http.Client? client = null)
        {
            client ??= HttpClient;
            int retries = 0;
            TOP:
            try
            {
                using var response = await client.GetAsync(url);
                await UpdateRemaining(response);
                if (!response.IsSuccessStatusCode)
                {
                    Utils.Log($"Nexus call failed: {response.RequestMessage!.RequestUri}");
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
                Utils.Log($"Nexus call failed `{url}`: " + e);
                throw;
            }
        }

        private async Task<T> GetCached<T>(string url)
        {
            if (BuildServerStatus.IsBuildServerDown)
                return await Get<T>(url);

            var builder = new UriBuilder(url)
            {
                Host = Consts.WabbajackBuildServerUri.Host, 
                Scheme = Consts.WabbajackBuildServerUri.Scheme, 
                Port = Consts.WabbajackBuildServerUri.Port
            };
            return await Get<T>(builder.ToString(), HttpClient.WithHeader((Consts.MetricsKeyHeader, await Metrics.GetMetricsKey())));
        }


        private static AsyncLock ManualDownloadLock = new();
        public async Task<string> GetNexusDownloadLink(NexusDownloader.State archive)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            
            var info = await GetModInfo(archive.Game, archive.ModID);
            var fileInfo = await GetModFiles(archive.Game, archive.ModID);
            if (!info.available || !fileInfo.files.Any(f => f.file_id == archive.FileID && f.category_name != null))
                throw new Exception("Mod unavailable");
            
            if (await IsPremium() && !ManualTestingMode)
            {
                if (HourlyRemaining <= 0 && DailyRemaining <= 0)
                {
                    throw new NexusAPIQuotaExceeded();
                }

                var url =
                    $"https://api.nexusmods.com/v1/games/{archive.Game.MetaData().NexusName}/mods/{archive.ModID}/files/{archive.FileID}/download_link.json";
                return (await Get<List<DownloadLink>>(url)).First().URI;
            }

            try
            {
                using var _ = await ManualDownloadLock.WaitAsync();
                await Task.Delay(1000);
                Utils.Log($"Requesting manual download for {archive.Name} {archive.PrimaryKeyString}");
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
                try
                {
                    return await GetCached<ModInfo>(url);
                }
                catch (HttpException)
                {
                    return await Get<ModInfo>(url);
                }
            }

            return await Get<ModInfo>(url);
        }

        private class DownloadLink
        {
            public string URI { get; set; } = string.Empty;
        }

        public static Uri ManualDownloadUrl(NexusDownloader.State state)
        {
            return new Uri($"https://www.nexusmods.com/{state.Game.MetaData().NexusName}/mods/{state.ModID}?tab=files");
        }
    }
}
