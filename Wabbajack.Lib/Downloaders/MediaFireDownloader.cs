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
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            Uri url = DownloaderUtils.GetDirectURL(archiveINI);
            if (url == null || url.Host != "www.mediafire.com") return null;

            return new State
            {
                Url = url.ToString()
            };
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }

            public override object[] PrimaryKey { get => new object[] {Url};}

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.AllowedPrefixes.Any(p => Url.StartsWith(p));
            }

            public override async Task<bool> Download(Archive a, string destination)
            {
                var result = await Resolve();
                return await result.Download(a, destination);
            }

            public override async Task<bool> Verify(Archive a)
            {
                return await Resolve() != null;
            }

            private async Task<HTTPDownloader.State> Resolve()
            {
                using (var d = await Driver.Create())
                {
                    await d.NavigateTo(new Uri("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.tx"));
                    // MediaFire creates the link after all the JS loads
                    await Task.Delay(1000);
                    var newURL = await d.GetAttr("a.input", "href");
                    if (newURL == null || !newURL.StartsWith("http")) return null;
                    return new HTTPDownloader.State()
                    {
                        Client = new Common.Http.Client(),
                        Url = newURL
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
