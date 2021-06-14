using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class VectorPlexusDownloader : AbstractIPS4OAuthDownloader<VectorPlexusDownloader, VectorPlexusDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "Vector Plexus";
        public override Uri SiteURL => new Uri("https://vectorplexus.com");
        public override Uri IconUri => new Uri("https://www.vectorplexus.com/favicon.ico");
        #endregion

        public VectorPlexusDownloader() : base("45c6d3c9867903a7daa6ded0a38cedf8", 
            new Uri("https://vectorplexus.com/oauth/authorize/"), new Uri("https://vectorplexus.com/oauth/token/"), 
            "vector-plexus-oauth2")
        {
        }


        public class State : AbstractIPS4OAuthDownloader<VectorPlexusDownloader, VectorPlexusDownloader.State>.State
        {
            public override object[] PrimaryKey { get; } = Array.Empty<object>();
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> Download(Archive a, AbsolutePath destination)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> Verify(Archive archive, CancellationToken? token = null)
            {
                throw new NotImplementedException();
            }

            public override IDownloader GetDownloader()
            {
                throw new NotImplementedException();
            }

            public override string? GetManifestURL(Archive a)
            {
                throw new NotImplementedException();
            }

            public override string[] GetMetaIni()
            {
                throw new NotImplementedException();
            }
        }
        
        /*
        [JsonName("VectorPlexusDownloader")]
        public class State //: State<VectorPlexusDownloader>
        {
            public override async Task<bool> LoadMetaData()
            {
                var html = await Downloader.AuthedClient.GetStringAsync(URL);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var node = doc.DocumentNode;

                Name = HttpUtility.HtmlDecode(node
                    .SelectNodes(
                        "//h1[@class='ipsType_pageTitle ipsContained_container']/span[@class='ipsType_break ipsContained']")
                    ?.First().InnerHtml);

                Author = HttpUtility.HtmlDecode(node
                    .SelectNodes(
                        "//div[@class='ipsBox_alt']/div[@class='ipsPhotoPanel ipsPhotoPanel_tiny ipsClearfix ipsSpacer_bottom']/div/p[@class='ipsType_reset ipsType_large ipsType_blendLinks']/a")
                    ?.First().InnerHtml);

                Version = HttpUtility.HtmlDecode(node
                    .SelectNodes("//section/h2[@class='ipsType_sectionHead']/span[@data-role='versionTitle']")
                    ?
                    .First().InnerHtml);

                var url = HttpUtility.HtmlDecode(node
                    .SelectNodes(
                        "//div[@class='ipsBox ipsSpacer_top ipsSpacer_double']/section/div[@class='ipsPad ipsAreaBackground']/div[@class='ipsCarousel ipsClearfix']/div[@class='ipsCarousel_inner']/ul[@class='cDownloadsCarousel ipsClearfix']/li[@class='ipsCarousel_item ipsAreaBackground_reset ipsPad_half']/span[@class='ipsThumb ipsThumb_medium ipsThumb_bg ipsCursor_pointer']")
                    ?.First().GetAttributeValue("data-fullurl", "none"));

                if (!string.IsNullOrWhiteSpace(url))
                {
                    ImageURL = new Uri(url);
                    return true;
                }

                url = HttpUtility.HtmlDecode(node
                    .SelectNodes(
                        "//article[@class='ipsColumn ipsColumn_fluid']/div[@class='ipsPad']/section/div[@class='ipsType_richText ipsContained ipsType_break']/p/a/img[@class='ipsImage ipsImage_thumbnailed']")
                    ?.First().GetAttributeValue("src", ""));
                if (!string.IsNullOrWhiteSpace(url))
                {
                    ImageURL = new Uri(url);
                }

                return true;
            }
        }
        */
    }
}
