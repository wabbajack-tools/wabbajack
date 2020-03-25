using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Exceptions;
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
            public override object[] PrimaryKey { get => new object[] {Id}; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.GoogleIDs.Contains(Id);
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                var state = await ToHttpState();
                return await state.Download(a, destination);
            }

            private async Task<HTTPDownloader.State> ToHttpState()
            {
                var initialURL = $"https://drive.google.com/uc?id={Id}&export=download";
                var client = new Common.Http.Client();
                using var response = await client.GetAsync(initialURL);
                if (!response.IsSuccessStatusCode)
                    throw new HttpException((int)response.StatusCode, response.ReasonPhrase);
                var regex = new Regex("(?<=/uc\\?export=download&amp;confirm=).*(?=;id=)");
                var confirm = regex.Match(await response.Content.ReadAsStringAsync());
                var url = $"https://drive.google.com/uc?export=download&confirm={confirm}&id={Id}";
                var httpState = new HTTPDownloader.State {Url = url, Client = client};
                return httpState;
            }

            public override async Task<bool> Verify(Archive a)
            {
                var state = await ToHttpState();
                return await state.Verify(a);
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<GoogleDriveDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return $"https://drive.google.com/uc?id={Id}&export=download";
            }

            public override string[] GetMetaIni()
            {
                return new [] {"[General]",$"directURL=https://drive.google.com/uc?id={Id}&export=download"};
            }
        }
    }
}
