using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using File = System.IO.File;

namespace Wabbajack.Lib.Downloaders
{
    // IPS4 is the site used by LoversLab, VectorPlexus, etc. the general mechanics of each site are the 
    // same, so we can fairly easily abstract the state.
    // Pass in the state type via TState
    public abstract class AbstractIPS4Downloader<TDownloader, TState> : AbstractNeedsLoginDownloader, IDownloader 
        where TState : AbstractIPS4Downloader<TDownloader, TState>.State<TDownloader>, new() 
        where TDownloader : IDownloader
    {
        public override string SiteName { get; }
        public override Uri SiteURL { get; }
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            Uri url = DownloaderUtils.GetDirectURL(archiveINI);
            if (url == null || url.Host != SiteURL.Host || !url.AbsolutePath.StartsWith("/files/file/")) return null;
            var id = HttpUtility.ParseQueryString(url.Query)["r"];
            var file = url.AbsolutePath.Split('/').Last(s => s != "");

            return new TState
            {
                FileID = id,
                FileName = file
            };
        }


        public class State<TDownloader> : AbstractDownloadState where TDownloader : IDownloader
        {
            public string FileID { get; set; }
            public string FileName { get; set; }

            public override object[] PrimaryKey
            {
                get
                {
                    if (FileID == null) 
                        return new object[] {Downloader.SiteURL, FileName};
                    return new object[] {Downloader.SiteURL, FileName, FileID};
                }
            }

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
                var downloader = (AbstractNeedsLoginDownloader)(object)DownloadDispatcher.GetInstance<TDownloader>();

                TOP:
                string csrfurl;
                if (FileID == null)
                {
                    csrfurl = $"https://{downloader.SiteURL.Host}/files/file/{FileName}/?do=download";
                }
                else
                {
                    csrfurl = $"https://{downloader.SiteURL.Host}/files/file/{FileName}/?do=download&r={FileID}";
                }
                var html = await downloader.AuthedClient.GetStringAsync(csrfurl);

                var pattern = new Regex("(?<=csrfKey=).*(?=[&\"\'])|(?<=csrfKey: \").*(?=[&\"\'])");
                var matches = pattern.Matches(html).Cast<Match>();
                    
                    var csrfKey = matches.Where(m => m.Length == 32).Select(m => m.ToString()).FirstOrDefault();

                if (csrfKey == null)
                    return null;

                string url;
                if (FileID == null)
                    url = $"https://{downloader.SiteURL.Host}/files/file/{FileName}/?do=download&confirm=1&t=1&csrfKey={csrfKey}";
                else
                    url = $"https://{downloader.SiteURL.Host}/files/file/{FileName}/?do=download&r={FileID}&confirm=1&t=1&csrfKey={csrfKey}";
                    

                var streamResult = await downloader.AuthedClient.GetAsync(url);
                if (streamResult.StatusCode != HttpStatusCode.OK)
                {
                    Utils.Error(new InvalidOperationException(), $"{downloader.SiteName} servers reported an error for file: {FileID}");
                }

                var content_type = streamResult.Content.Headers.ContentType;

                if (content_type.MediaType == "application/json")
                {
                    // Sometimes LL hands back a json object telling us to wait until a certain time
                    var times = (await streamResult.Content.ReadAsStringAsync()).FromJSONString<WaitResponse>();
                    var secs = times.Download - times.CurrentTime;
                    for (int x = 0; x < secs; x++)
                    {
                        Utils.Status($"Waiting for {secs} at the request of {downloader.SiteName}", x * 100 / secs);
                        await Task.Delay(1000);
                    }
                    Utils.Status("Retrying download");
                    goto TOP;
                }

                return await streamResult.Content.ReadAsStreamAsync();
            }

            private class WaitResponse
            {
                [JsonProperty("download")]
                public int Download { get; set; }
                [JsonProperty("currentTime")]
                public int CurrentTime { get; set; }
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
                return DownloadDispatcher.GetInstance<TDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                var downloader = (INeedsLogin)GetDownloader();
                return $"* {((INeedsLogin)GetDownloader()).SiteName} - [{a.Name}](https://{downloader.SiteURL.Host}/files/file/{FileName}/?do=download&r={FileID})";
            }

            public override string[] GetMetaIni()
            {
                var downloader = Downloader;
                
                if (FileID != null)
                    return new[]
                    {
                        "[General]",
                        $"directURL=https://{downloader.SiteURL.Host}/files/file/{FileName}/?do=download&r={FileID}&confirm=1&t=1"
                    };
                return new[]
                {
                    "[General]",
                    $"directURL=https://{downloader.SiteURL.Host}/files/file/{FileName}"
                };
            }

            private static AbstractNeedsLoginDownloader Downloader => (AbstractNeedsLoginDownloader)(object)DownloadDispatcher.GetInstance<TDownloader>();
        }

        protected AbstractIPS4Downloader(Uri loginUri, string encryptedKeyName, string cookieDomain) : 
            base(loginUri, encryptedKeyName, cookieDomain, "ips4_member_id")
        {
        }


    }
}
