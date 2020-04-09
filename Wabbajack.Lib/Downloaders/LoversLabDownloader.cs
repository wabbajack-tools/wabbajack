using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.Downloaders
{
    public class LoversLabDownloader : AbstractIPS4Downloader<LoversLabDownloader, LoversLabDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "Lovers Lab";
        public override Uri SiteURL => new Uri("https://www.loverslab.com");
        public override Uri IconUri => new Uri("https://www.loverslab.com/favicon.ico");
        #endregion
        public LoversLabDownloader() : base(new Uri("https://www.loverslab.com/login"), 
            "loverslabcookies", "loverslab.com")
        {
        }
        protected override async Task WhileWaiting(IWebDriver browser)
        {
            try
            {
                await browser.EvaluateJavaScript(
                    "document.querySelectorAll(\".ll_adblock\").forEach(function (itm) { itm.innerHTML = \"\";});");
            }
            catch (Exception ex)
            {
                Utils.Error(ex);
            }
        }

        
        [JsonName("LoversLabDownloader")]
        public class State : State<LoversLabDownloader>
        {
            [JsonIgnore]
            public override bool IsNSFW => true;

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
                
                ImageURL = HttpUtility.HtmlDecode(node
                    .SelectNodes(
                        "//div[@class='ipsBox ipsSpacer_top ipsSpacer_double']/section/div[@class='ipsPad ipsAreaBackground']/div[@class='ipsCarousel ipsClearfix']/div[@class='ipsCarousel_inner']/ul[@class='cDownloadsCarousel ipsClearfix']/li[@class='ipsCarousel_item ipsAreaBackground_reset ipsPad_half']/span[@class='ipsThumb ipsThumb_medium ipsThumb_bg ipsCursor_pointer']")
                    ?.First().GetAttributeValue("data-fullurl", "none"));

                if (!string.IsNullOrWhiteSpace(ImageURL))
                    return true;

                ImageURL = HttpUtility.HtmlDecode(node
                    .SelectNodes(
                        "//article[@class='ipsColumn ipsColumn_fluid']/div[@class='ipsPad']/section/div[@class='ipsType_richText ipsContained ipsType_break']/p/a/img[@class='ipsImage ipsImage_thumbnailed']")
                    ?.First().GetAttributeValue("src", ""));
                if (string.IsNullOrWhiteSpace(ImageURL))
                    ImageURL = "";

                return true;
            }
        }
    }
}
