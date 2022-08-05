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
    public class VectorPlexusOAuthDownloader : AbstractIPS4OAuthDownloader<VectorPlexusOAuthDownloader, VectorPlexusOAuthDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "Vector Plexus";
        public override Uri SiteURL => new Uri("https://vectorplexus.com");
        public override Uri IconUri => new Uri("https://www.vectorplexus.com/favicon.ico");
        #endregion

        public VectorPlexusOAuthDownloader() : base("45c6d3c9867903a7daa6ded0a38cedf8", 
            new Uri("https://vectorplexis.com/oauth/authorize/"), 
            new Uri("https://vectorplexis.com/oauth/token/"), 
            new []{"profile", "get_downloads"},
            "vector-plexus-oauth2")
        {
        }


        [JsonName("VectorPlexusOAuthDownloader+State")]
        public class State :  AbstractIPS4OAuthDownloader<VectorPlexusOAuthDownloader, VectorPlexusOAuthDownloader.State>.State
        {
            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<VectorPlexusOAuthDownloader>();
            }
        }
    }
}
