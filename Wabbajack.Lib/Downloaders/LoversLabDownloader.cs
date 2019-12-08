using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Common.StatusFeed.Errors;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;
using Xilium.CefGlue.Common;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Lib.Downloaders
{
    public class LoversLabDownloader : IDownloader
    {
        internal HttpClient _authedClient;

        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {

            Uri url = DownloaderUtils.GetDirectURL(archive_ini);
            if (url == null || url.Host != "www.loverslab.com" && url.AbsolutePath.StartsWith("/files/file/")) return null;
            var id = HttpUtility.ParseQueryString(url.Query)["r"];
            var file = url.AbsolutePath.Split('/').Last(s => s != "");

            return new State
            {
                FileID = id,
                FileName = file
            };
        }

        public void Prepare()
        {
            _authedClient = GetAuthedClient().Result ?? throw new Exception("not logged into LL, TODO");
        }

        public static async Task<Helpers.Cookie[]> GetAndCacheLoversLabCookies(BaseCefBrowser browser, Action<string> updateStatus)
        {
            updateStatus("Please Log Into Lovers Lab");
            browser.Address = "https://www.loverslab.com/login";

            async Task<bool> CleanAds()
            {
                try
                {
                   // await browser.EvaluateJavaScript<string>(
                    //    "document.querySelectorAll(\"iframe\").forEach(function(itm) { itm.src = \"about:blank\"})");
                    await browser.EvaluateJavaScript<string>(
                        "document.querySelectorAll(\".ad\").forEach(function (itm) { itm.innerHTML = \"\";});");
                }
                catch (Exception ex)
                {
                }
                return false;
            }
            var cookies = new Helpers.Cookie[0];
            while (true)
            {
                await CleanAds();
                cookies = (await Helpers.GetCookies("loverslab.com"));
                if (cookies.FirstOrDefault(c => c.Name == "ips4_member_id") != null)
                    break;
                await Task.Delay(500);
            }

            cookies.ToEcryptedJson("loverslabcookies");

            return cookies;
        }

        public async Task<HttpClient> GetAuthedClient()
        {
            Helpers.Cookie[] cookies;
            try
            {
                cookies = Utils.FromEncryptedJson<Helpers.Cookie[]>("loverslabcookies");
                if (cookies != null)
                    return Helpers.GetClient(cookies, "https://www.loverslab.com");
            }
            catch (FileNotFoundException) { }

            cookies = Utils.Log(new RequestLoversLabLogin()).Task.Result;
            return Helpers.GetClient(cookies, "https://www.loverslab.com");
        }

        public class State : AbstractDownloadState
        {
            public string FileID { get; set; }
            public string FileName { get; set; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override void Download(Archive a, string destination)
            {
                var stream = ResolveDownloadStream().Result;
                using (var file = File.OpenWrite(destination))
                {
                    stream.CopyTo(file);
                }
            }

            private async Task<Stream> ResolveDownloadStream()
            {
                var result = DownloadDispatcher.GetInstance<LoversLabDownloader>();
                TOP:
                var html = await result._authedClient.GetStringAsync(
                    $"https://www.loverslab.com/files/file/{FileName}/?do=download&r={FileID}");

                var pattern = new Regex("(?<=csrfKey=).*(?=[&\"\'])");
                var csrfKey = pattern.Matches(html).Cast<Match>().Where(m => m.Length == 32).Select(m => m.ToString()).FirstOrDefault();

                if (csrfKey == null)
                    return null;

                var url =
                    $"https://www.loverslab.com/files/file/{FileName}/?do=download&r={FileID}&confirm=1&t=1&csrfKey={csrfKey}";

                var streamResult = await result._authedClient.GetAsync(url);
                if (streamResult.StatusCode != HttpStatusCode.OK)
                {
                    Utils.Error(new InvalidOperationException(), $"LoversLab servers reported an error for file: {FileID}");
                }

                var content_type = streamResult.Content.Headers.ContentType;

                if (content_type.MediaType == "application/json")
                {
                    // Sometimes LL hands back a json object telling us to wait until a certain time
                    var times = (await streamResult.Content.ReadAsStringAsync()).FromJSONString<WaitResponse>();
                    var secs = times.download - times.currentTime;
                    for (int x = 0; x < secs; x++)
                    {
                        Utils.Status($"Waiting for {secs} at the request of LoversLab", x * 100 / secs);
                        await Task.Delay(1000);
                    }
                    Utils.Status("Retrying download");
                    goto TOP;
                }

                return await streamResult.Content.ReadAsStreamAsync();
            }

            internal class WaitResponse
            {
                public int download { get; set; }
                public int currentTime { get; set; }
            }

            public override bool Verify()
            {
                var stream = ResolveDownloadStream().Result;
                if (stream == null)
                {
                    return false;
                }

                stream.Close();
                return true;

            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<LoversLabDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* Lovers Lab - [{a.Name}](https://www.loverslab.com/files/file/{FileName}/?do=download&r={FileID})";
            }
        }
    }

    public class RequestLoversLabLogin : AStatusMessage, IUserIntervention
    {
        public override string ShortDescription => "Getting LoversLab information";
        public override string ExtendedDescription { get; }

        private readonly TaskCompletionSource<Helpers.Cookie[]> _source = new TaskCompletionSource<Helpers.Cookie[]>();
        public Task<Helpers.Cookie[]> Task => _source.Task;

        public void Resume(Helpers.Cookie[] cookies)
        {
            _source.SetResult(cookies);
        }
        public void Cancel()
        {
            _source.SetCanceled();
        }
    }
}
