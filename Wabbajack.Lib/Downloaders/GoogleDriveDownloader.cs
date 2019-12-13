using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class GoogleDriveDownloader : IDownloader, IUrlDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url != null && url.StartsWith("https://drive.google.com"))
            {
                var regex = new Regex("((?<=id=)[a-zA-Z0-9_-]*)|(?<=\\/file\\/d\\/)[a-zA-Z0-9_-]*");
                var match = regex.Match(url);
                return new State
                {
                    Id = match.ToString()
                };
            }

            return null;
        }

        public async Task Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Id { get; set; }
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.GoogleIDs.Contains(Id);
            }

            public override async Task Download(Archive a, string destination)
            {
                var state = await ToHttpState();
                await state.Download(a, destination);
            }

            private async Task<HTTPDownloader.State> ToHttpState()
            {
                var initialURL = $"https://drive.google.com/uc?id={Id}&export=download";
                var client = new HttpClient();
                var result = await client.GetStringAsync(initialURL);
                var regex = new Regex("(?<=/uc\\?export=download&amp;confirm=).*(?=;id=)");
                var confirm = regex.Match(result);
                var url = $"https://drive.google.com/uc?export=download&confirm={confirm}&id={Id}";
                var httpState = new HTTPDownloader.State {Url = url, Client = client};
                return httpState;
            }

            public override async Task<bool> Verify()
            {
                var state = await ToHttpState();
                return await state.Verify();
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<GoogleDriveDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* GoogleDrive - [{a.Name}](https://drive.google.com/uc?id={Id}&export=download)";
            }
        }
    }
}
