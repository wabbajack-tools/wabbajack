using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class YandexDownloader : IUrlDownloader
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false)
        {
            var uri = (Uri?)DownloaderUtils.GetDirectURL(archiveINI);
            if (uri == null) return null;

            return uri.Host == "yadi.sk" ? new State(uri) : null;
        }

        public async Task Prepare()
        {
        }

        public AbstractDownloadState? GetDownloaderState(string url)
        {
            var uri = new Uri(url);
            if (uri.Host == "yadi.sk")
            {
                return new State(uri);
            }

            return null;
        }

        [JsonName("YandexDownloader+State")]
        public class State : AbstractDownloadState
        {
            public Uri Url { get; set; }

            public State(Uri url)
            {
                Url = url;
            }
            public override object[] PrimaryKey => new object[] {Url};
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                var url = Url.ToString();
                return whitelist.AllowedPrefixes.Any(p => url.StartsWith(p));
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                using var driver = await WebAutomation.Driver.Create();
                var tcs = new TaskCompletionSource<Uri?>();
                driver.DownloadHandler = uri => tcs.SetResult(uri);
                await driver.NavigateTo(Url);
                await driver.EvalJavascript("document.getElementsByClassName(\"download-button\")[0].click();");
                var uri = await tcs.Task;
                return await new HTTPDownloader.State(uri!.ToString()).Download(destination);
            }

            public override async Task<bool> Verify(Archive archive)
            {
                var client = new Wabbajack.Lib.Http.Client();
                var result = await client.GetAsync(Url, errorsAsExceptions: false);
                return result.IsSuccessStatusCode;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<YandexDownloader>();
            }

            public override string? GetManifestURL(Archive a)
            {
                return Url.ToString();
            }

            public override string[] GetMetaIni()
            {
                return new[] {"[General]", $"directURL={Url}"};
            }
        }
    }
}
