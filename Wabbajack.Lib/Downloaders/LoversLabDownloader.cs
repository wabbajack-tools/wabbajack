using System;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Wabbajack.Common;
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

        public class State : State<LoversLabDownloader>
        {
            public override bool IsNSFW => true;

            public override async Task<bool> LoadMetaData()
            {
                var html = await Downloader.AuthedClient.GetStringAsync(URL);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var node = doc.DocumentNode;
                Name = node.SelectNodes("//h1[@class='ipsType_pageTitle ipsContained_container']/span")?.First().InnerHtml;
                Author = node
                    .SelectNodes(
                        "//div[@class='ipsBox_alt']/div[@class='ipsPhotoPanel ipsPhotoPanel_tiny ipsClearfix ipsSpacer_bottom']/div/p[@class='ipsType_reset ipsType_large ipsType_blendLinks']/a")
                    ?.First().InnerHtml;
                Version = node.SelectNodes("//section/h2[@class='ipsType_sectionHead']/span[@data-role='versionTitle']")
                    ?
                    .First().InnerHtml;
                ImageURL = node
                    .SelectNodes(
                        "//div[@class='ipsBox ipsSpacer_top ipsSpacer_double']/section/div[@class='ipsPad ipsAreaBackground']/div[@class='ipsCarousel ipsClearfix']/div[@class='ipsCarousel_inner']/ul[@class='cDownloadsCarousel ipsClearfix']/li[@class='ipsCarousel_item ipsAreaBackground_reset ipsPad_half']/span[@class='ipsThumb ipsThumb_medium ipsThumb_bg ipsCursor_pointer']")
                    ?.First().GetAttributeValue("data-fullurl", "none");
                return true;
            }
        }
    }
}
