using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class GoogleDriveDownloader : IDownloader, IUrlDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.directURL;
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

        public void Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Id { get; set; }
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.GoogleIDs.Contains(Id);
            }

            public override void Download(Archive a, string destination)
            {
                ToHttpState().Download(a, destination);
            }

            private HTTPDownloader.State ToHttpState()
            {
                var initial_url = $"https://drive.google.com/uc?id={Id}&export=download";
                var client = new HttpClient();
                var result = client.GetStringSync(initial_url);
                var regex = new Regex("(?<=/uc\\?export=download&amp;confirm=).*(?=;id=)");
                var confirm = regex.Match(result);
                var url = $"https://drive.google.com/uc?export=download&confirm={confirm}&id={Id}";
                var http_state = new HTTPDownloader.State {Url = url, Client = client};
                return http_state;
            }

            public override bool Verify()
            {
                return ToHttpState().Verify();
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
