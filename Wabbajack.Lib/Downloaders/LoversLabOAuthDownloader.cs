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
    public class LoversLabOAuthDownloader : AbstractIPS4OAuthDownloader<LoversLabOAuthDownloader, LoversLabOAuthDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "Lovers Lab";
        public override Uri SiteURL => new("https://loverslab.com");
        public override Uri IconUri => new("https://www.loverslab.com/favicon.ico");
        #endregion

        public LoversLabOAuthDownloader() : base("0b543a010bf1a8f0f4c5dae154fce7c3", 
            new Uri("https://api.loverslab.com/oauth/authorize/"), 
            new Uri("https://api.loverslab.com/oauth/token/"), 
            new []{"downloads"},
            "lovers-lab-oauth2")
        {
        }


        [JsonName("LoversLabOAuthDownloader")]
        public class State :  AbstractIPS4OAuthDownloader<LoversLabOAuthDownloader, LoversLabOAuthDownloader.State>.State
        {
            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<LoversLabOAuthDownloader>();
            }
        }
    }
}
