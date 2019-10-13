using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Validation;

namespace Wabbajack.Downloaders
{
    public class ModDBDownloader : IDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.directURL;

            if (url != null && url.StartsWith("https://www.moddb.com/downloads/start"))
            {
                return new State
                {
                    Url = url
                };
            }

            return null;
        }

        public void Prepare()
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

            public override void Download(Archive a, string destination)
            {
                var new_url = GetDownloadUrl();
                new HTTPDownloader.State {Url = new_url}.Download(a, destination);
            }

            private string GetDownloadUrl()
            {
                var client = new HttpClient();
                var result = client.GetStringSync(Url);
                var regex = new Regex("https:\\/\\/www\\.moddb\\.com\\/downloads\\/mirror\\/.*(?=\\\")");
                var match = regex.Match(result);
                var new_url = match.Value;
                return new_url;
            }

            public override bool Verify()
            {
                var new_url = GetDownloadUrl();
                return new HTTPDownloader.State { Url = new_url }.Verify();
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
