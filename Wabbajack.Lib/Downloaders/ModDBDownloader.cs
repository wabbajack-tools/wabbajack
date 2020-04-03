using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using MessagePack;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class ModDBDownloader : IDownloader, IUrlDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url != null && url.StartsWith("https://www.moddb.com/downloads/start"))
            {
                return new State
                {
                    Url = url
                };
            }

            return null;
        }

        public async Task Prepare()
        {
        }

        [MessagePackObject]
        public class State : AbstractDownloadState
        {
            
            [Key(0)]
            public string Url { get; set; }
            
            [IgnoreMember]
            public override object[] PrimaryKey { get => new object[]{Url}; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                // Everything from Moddb is whitelisted
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                var urls = await GetDownloadUrls();
                Utils.Log($"Found {urls.Length} ModDB mirrors for {a.Name}");
                foreach (var (url, idx) in urls.Zip(Enumerable.Range(0, urls.Length), (s, i) => (s, i)))
                {
                    try
                    {
                        await new HTTPDownloader.State {Url = url}.Download(a, destination);
                        return true;
                    }
                    catch (Exception)
                    {
                        if (idx == urls.Length - 1)
                            throw;
                        Utils.Log($"Download from {url} failed, trying next mirror");
                    }
                }
                return false;
            }

            private async Task<string[]> GetDownloadUrls()
            {
                var uri = new Uri(Url);
                var modId = uri.AbsolutePath.Split('/').Reverse().First(f => int.TryParse(f, out int _));
                var mirrorUrl = $"https://www.moddb.com/downloads/start/{modId}/all";
                var doc = await new HtmlWeb().LoadFromWebAsync($"https://www.moddb.com/downloads/start/{modId}/all");
                var mirrors = doc.DocumentNode.Descendants().Where(d => d.NodeType == HtmlNodeType.Element && d.HasClass("row"))
                    .Select(d => new
                    {
                        Link = "https://www.moddb.com"+
                               d.Descendants().Where(s => s.Id == "downloadon")
                            .Select(i => i.GetAttributeValue("href", ""))
                            .FirstOrDefault(),
                        Load = d.Descendants().Where(s => s.HasClass("subheading"))
                            .Select(i => i.InnerHtml.Split(',')
                                .Last()
                                .Split('%')
                                .Select(v => double.TryParse(v, out var dr) ? dr : double.MaxValue)
                                .First())
                            .FirstOrDefault()
                    })
                    .OrderBy(d => d.Load)
                    .ToList();
                
                return mirrors.Select(d => d.Link).ToArray();
            }

            public override async Task<bool> Verify(Archive a)
            {
                await GetDownloadUrls();
                return true;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<ModDBDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return Url;
            }

            public override string[] GetMetaIni()
            {
                return new[] {"[General]", $"directURL={Url}"};
            }
        }
    }
}
