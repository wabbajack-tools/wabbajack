using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using CefSharp;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;
using File = Alphaleonis.Win32.Filesystem.File;

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
        }
    }
}
