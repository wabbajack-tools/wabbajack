using System;
using System.Threading.Tasks;
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

        public override string ModNameRegex => "";

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
        public class State : State<LoversLabDownloader>, IMetaState
        {
            public string URL => $"{Site}/files/file/{FileName}";
            public string Name { get; }
            public string Author { get; }
            public string Version { get; }
            public string ImageURL { get; }
            public bool IsNSFW => true;
            public string Description { get; }
        }
    }
}
