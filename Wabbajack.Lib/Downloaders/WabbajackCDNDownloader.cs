using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.Downloaders.UrlDownloaders;
using Wabbajack.Lib.Exceptions;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class WabbajackCDNDownloader : IDownloader
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false)
        {
            var url = (Uri)DownloaderUtils.GetDirectURL(archiveINI);
            return url == null ? null : StateFromUrl(url);
        }

        public async Task Prepare()
        {
        }

       
        public static AbstractDownloadState? StateFromUrl(Uri url)
        {
            if (url.Host == "wabbajacktest.b-cdn.net" || url.Host == "wabbajack.b-cdn.net")
            {
                return new State(url);
            }
            return null;
        }

        [JsonName("WabbajackCDNDownloader+State")]
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
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                var definition = await GetDefinition();
                await using var fs = destination.Create();
                var client = new Common.Http.Client();
                await definition.Parts.DoProgress($"Downloading {a.Name}", async part =>
                {
                    fs.Position = part.Offset;
                    using var response = await client.GetAsync($"{Url}/parts/{part.Index}");
                    if (!response.IsSuccessStatusCode)
                        throw new HttpException((int)response.StatusCode, response.ReasonPhrase);
                    await response.Content.CopyToAsync(fs);
                });
                return true;
            }

            public override async Task<bool> Verify(Archive archive)
            {
                var definition = await GetDefinition();
                return true;
            }

            private async Task<CDNFileDefinition> GetDefinition()
            {
                var client = new Common.Http.Client();
                using var data = await client.GetAsync(Url + "/definition.json.gz");
                await using var gz = new GZipStream(await data.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
                return gz.FromJson<CDNFileDefinition>();
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<WabbajackCDNDownloader>();
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
