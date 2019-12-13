using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.Validation;
using Xilium.CefGlue.Common;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Lib.Downloaders
{
    public class DeadlyStreamDownloader : IDownloader
    {
        internal HttpClient AuthedClient;

        public AbstractDownloadState GetDownloaderState(dynamic archiveINI)
        {
            Uri url = DownloaderUtils.GetDirectURL(archiveINI);
            if (url == null || url.Host != "www.deadlystream.com" ||
                !url.AbsolutePath.StartsWith("/files/file/")) return null;
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
            AuthedClient = GetAuthedClient().Result ?? throw new Exception("Not logged into DeadlyStream!");
        }

        public static async Task<Helpers.Cookie[]> GetAndCacheDeadlyStreamCookies(BaseCefBrowser browser,
            Action<string> updateStatus, CancellationToken cancel)
        {
            updateStatus("Please log into Deadly Stream");
            browser.Address = "https://www.deadlystream.com/login";

            async Task CleanAds()
            {
                try
                {
                    await browser.EvaluateJavaScript<string>(
                        "document.querySelectorAll(\".adsbygoogle\").forEach(function (itm) { itm.innerHTML = \"\";});");
                }
                catch (Exception e)
                {
                    Utils.Error(e);
                }

                return;
            }

            var cookies = new Helpers.Cookie[0];
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                await CleanAds();
                cookies = await Helpers.GetCookies("deadlystream.com");
                if (cookies.FirstOrDefault(c => c.Name == "ips4_member_id") != null)
                    break;
                await Task.Delay(500, cancel);
            }

            cookies.ToEcryptedJson("deadlystreamcookies");
            return cookies;
        }

        public async Task<HttpClient> GetAuthedClient()
        {
            Helpers.Cookie[] cookies;
            try
            {
                cookies = Utils.FromEncryptedJson<Helpers.Cookie[]>("deadlystreamcookies");
                if (cookies != null)
                    return Helpers.GetClient(cookies, "https://www.deadlystream.com");
            }
            catch (FileNotFoundException) { }

            cookies = Utils.Log(new RequestDeadlyStreamLogin()).Task.Result;
            return Helpers.GetClient(cookies, "https://www.deadlystream.com");
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
                var result = DownloadDispatcher.GetInstance<DeadlyStreamDownloader>();
                TOP:
                var url = "https://www.deadlystream.com/files/file/{FileName}/?do=download";
                if (FileID != null)
                    url += $"&r={FileID}";
                var html = await result.AuthedClient.GetStringAsync(url);

                var pattern = new Regex("(?<=csrfKey=).*(?=[&\"\'])");
                var csrfKey = pattern.Matches(html).Cast<Match>().Where(m => m.Length == 32).Select(m => m.ToString())
                    .FirstOrDefault();

                if (csrfKey == null)
                    return null;

                url += $"&confirm=1&t=1&csrfKey={csrfKey}";

                var streamResult = await result.AuthedClient.GetAsync(url);
                if (streamResult.StatusCode != HttpStatusCode.OK)
                {
                    Utils.Error(new InvalidOperationException(), $"DeadlyStream servers reported an error for file: {FileName}");
                }

                var contentType = streamResult.Content.Headers.ContentType;
                if (contentType.MediaType != "application/json")
                    return await streamResult.Content.ReadAsStreamAsync();
                //TODO
                return null;
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
                return DownloadDispatcher.GetInstance<DeadlyStreamDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return FileID == null
                    ? $"* DeadlyStream - [{a.Name}](https://www.deadlystream.com/files/file/{FileName}/?do=download)"
                    : $"* DeadlyStream - [{a.Name}](https://www.deadlystream.com/files/file/{FileName}/?do=download&r={FileID})";
            }
        }
    }

    public class RequestDeadlyStreamLogin : AUserIntervention
    {
        public override string ShortDescription => "Getting DeadlyStream information";
        public override string ExtendedDescription { get; }

        private readonly TaskCompletionSource<Helpers.Cookie[]> _source = new TaskCompletionSource<Helpers.Cookie[]>();
        public Task<Helpers.Cookie[]> Task => _source.Task;

        public void Resume(Helpers.Cookie[] cookies)
        {
            Handled = true;
            _source.SetResult(cookies);
        }

        public override void Cancel()
        {
            Handled = true;
            _source.SetCanceled();
        }
    }
}
