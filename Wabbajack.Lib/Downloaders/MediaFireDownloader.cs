using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;

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

            public override async Task<bool> Verify(Archive a, CancellationToken? token)
            {
                return await Resolve(token) != null;
            }

            private async Task<HTTPDownloader.State?> Resolve(CancellationToken? token = null)
            {
                var client = new Http.Client();
                var result = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, token:token);
                if (!result.IsSuccessStatusCode)
                    return null;

                if (result.Content.Headers.ContentType!.MediaType!.StartsWith("text/html",
                    StringComparison.OrdinalIgnoreCase))
                {
                    var body = await client.GetHtmlAsync(Url);
                    var node = body.DocumentNode.DescendantsAndSelf().First(d => d.HasClass("input") && d.HasClass("popsok") &&
                                                                                 d.GetAttributeValue("aria-label", "") == "Download file");
                    return new HTTPDownloader.State(node.GetAttributeValue("href", "not-found"));
                }

                return new HTTPDownloader.State(Url);
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
