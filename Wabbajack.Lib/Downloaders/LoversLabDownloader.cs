using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using CefSharp;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Lib.Downloaders
{
    public class LoversLabDownloader : AbstractNeedsLoginDownloader, IDownloader
    {
        #region INeedsDownload
        public override string SiteName => "Lovers Lab";
        public override Uri SiteURL => new Uri("https://loverslab.com");
        public override Uri IconUri => new Uri("https://www.loverslab.com/favicon.ico");
        #endregion

        public LoversLabDownloader() : base(new Uri("https://www.loverslab.com/login"), 
            "loverslabcookies", "loverslab.com", "ips4_member_id")
        {
        }


        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archive_ini)
        {
            Uri url = DownloaderUtils.GetDirectURL(archive_ini);
            if (url == null || url.Host != "www.loverslab.com" || !url.AbsolutePath.StartsWith("/files/file/")) return null;
            var id = HttpUtility.ParseQueryString(url.Query)["r"];
            var file = url.AbsolutePath.Split('/').Last(s => s != "");

            return new State
            {
                FileID = id,
                FileName = file
            };
        }
        protected override async Task WhileWaiting(IWebDriver browser)
        {
            try
            {
                await browser.EvaluateJavaScript(
                    "document.querySelectorAll(\".ll_adblock\").forEach(function (itm) { itm.innerHTML = \"\";});");
            }
            catch (Exception ex)
            {
                Utils.Error(ex);
            }
        }

        public class State : AbstractDownloadState
        {
            public string FileID { get; set; }
            public string FileName { get; set; }

            public override object[] PrimaryKey { get => new object[] {FileID, FileName}; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task Download(Archive a, string destination)
            {
                var stream = await ResolveDownloadStream();
                using (var file = File.OpenWrite(destination))
                {
                    stream.CopyTo(file);
                }
            }

            private async Task<Stream> ResolveDownloadStream()
            {
                var result = DownloadDispatcher.GetInstance<LoversLabDownloader>();
                TOP:
                var html = await result.AuthedClient.GetStringAsync(
                    $"https://www.loverslab.com/files/file/{FileName}/?do=download&r={FileID}");

                var pattern = new Regex("(?<=csrfKey=).*(?=[&\"\'])");
                var csrfKey = pattern.Matches(html).Cast<Match>().Where(m => m.Length == 32).Select(m => m.ToString()).FirstOrDefault();

                if (csrfKey == null)
                    return null;

                var url =
                    $"https://www.loverslab.com/files/file/{FileName}/?do=download&r={FileID}&confirm=1&t=1&csrfKey={csrfKey}";

                var streamResult = await result.AuthedClient.GetAsync(url);
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

            public override async Task<bool> Verify()
            {
                var stream = await ResolveDownloadStream();
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
}
