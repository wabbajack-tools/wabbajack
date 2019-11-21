using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.Downloaders
{
    public class MediaFireDownloader : IUrlDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            Uri url = DownloaderUtils.GetDirectURL(archive_ini);
            if (url == null || url.Host != "www.mediafire.com") return null;

            return new State
            {
                Url = url.ToString()
            };
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.AllowedPrefixes.Any(p => Url.StartsWith(p));
            }

            public override void Download(Archive a, string destination)
            {
                Resolve().Result.Download(a, destination);
            }

            public override bool Verify()
            {
                return Resolve().Result != null;
            }

            private async Task<HTTPDownloader.State> Resolve()
            {
                using (var d = await Driver.Create())
                {
                    await d.NavigateTo(new Uri("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.tx"));
                    // MediaFire creates the link after all the JS loads
                    await Task.Delay(1000);
                    var new_url = await d.GetAttr("a.input", "href");
                    if (new_url == null || !new_url.StartsWith("http")) return null;
                    return new HTTPDownloader.State()
                    {
                        Client = new HttpClient(),
                        Url = new_url
                    };
                }
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<MediaFireDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* [{a.Name} - {Url}]({Url})";
            }


        }

        public void Prepare()
        {
        }

        public AbstractDownloadState GetDownloaderState(string u)
        {
            var url = new Uri(u);
            if (url.Host != "www.mediafire.com") return null;

            return new State
            {
                Url = url.ToString()
            };
        }
    }
}
