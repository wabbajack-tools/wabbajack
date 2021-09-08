using System;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib.Downloaders
{
    public class LoversLabOAuthDownloader : AbstractIPS4OAuthDownloader<LoversLabOAuthDownloader, LoversLabOAuthDownloader.LoversLabState>
    {
        #region INeedsDownload
        public override string SiteName => "Lovers Lab";
        public override Uri SiteURL => new("https://www.loverslab.com");
        public override Uri IconUri => new("https://www.loverslab.com/favicon.ico");
        #endregion

        public LoversLabOAuthDownloader() : base("0b543a010bf1a8f0f4c5dae154fce7c3",
            new Uri("https://www.loverslab.com/oauth/authorize/"),
            new Uri("https://www.loverslab.com/oauth/token/"),
            new[] { "downloads" },
            "lovers-lab-oauth2")
        {
        }

        [JsonName("LoversLabOAuthDownloader")]
        public class LoversLabState : State
        {
            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<LoversLabOAuthDownloader>();
            }
        }
    }
}
