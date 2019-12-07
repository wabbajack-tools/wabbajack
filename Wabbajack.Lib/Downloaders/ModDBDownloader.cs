using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class ModDBDownloader : IDownloader, IUrlDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archiveINI)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url != null && url.StartsWith("https://www.moddb.com/downloads/start"))
            {
                return new State
                {
                    Url = url
                };
            }

            return null;
        }

        public async Task Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                // Everything from Moddb is whitelisted
                return true;
            }

            public override async Task Download(Archive a, string destination)
            {
                var newURL = await GetDownloadUrl();
                await new HTTPDownloader.State {Url = newURL}.Download(a, destination);
            }

            private async Task<string> GetDownloadUrl()
            {
                var client = new HttpClient();
                var result = await client.GetStringAsync(Url);
                var regex = new Regex("https:\\/\\/www\\.moddb\\.com\\/downloads\\/mirror\\/.*(?=\\\")");
                var match = regex.Match(result);
                var newURL = match.Value;
                return newURL;
            }

            public override async Task<bool> Verify()
            {
                var newURL = await GetDownloadUrl();
                return await new HTTPDownloader.State { Url = newURL }.Verify();
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<ModDBDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* ModDB - [{a.Name}]({Url})";
            }
        }
    }
}
