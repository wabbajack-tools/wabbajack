using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.Downloaders
{
    public class MediaFireDownloader : IUrlDownloader
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            Uri url = DownloaderUtils.GetDirectURL(archiveINI);
            if (url == null || url.Host != "www.mediafire.com") return null;

            return new State(url.ToString());
        }

        [JsonName("MediaFireDownloader+State")]
        public class State : AbstractDownloadState
        {
            public string Url { get; }

            public override object[] PrimaryKey => new object[] { Url };

            public State(string url)
            {
                Url = url;
            }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.AllowedPrefixes.Any(p => Url.StartsWith(p));
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                var result = await Resolve();
                if (result == null) return false;
                return await result.Download(a, destination);
            }

            public override async Task<bool> Verify(Archive a)
            {
                return await Resolve() != null;
            }

            private async Task<HTTPDownloader.State?> Resolve()
            {
                using (var d = await Driver.Create())
                {
                    await d.NavigateTo(new Uri("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.tx"));
                    // MediaFire creates the link after all the JS loads
                    await Task.Delay(1000);
                    var newURL = await d.GetAttr("a.input", "href");
                    if (newURL == null || !newURL.StartsWith("http")) return null;
                    return new HTTPDownloader.State(newURL)
                    {
                        Client = new Common.Http.Client(),
                    };
                }
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<MediaFireDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return Url;
            }

            public override string[] GetMetaIni()
            {
                return new []
                {
                    "[General]",
                    $"directURL={Url}"
                };
            }
        }

        public async Task Prepare()
        {
        }

        public AbstractDownloadState? GetDownloaderState(string u)
        {
            var url = new Uri(u);
            if (url.Host != "www.mediafire.com") return null;

            return new State(url.ToString());
        }
    }
}
