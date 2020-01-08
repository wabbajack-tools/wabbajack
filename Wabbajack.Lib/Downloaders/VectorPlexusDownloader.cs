using System;
using System.Threading.Tasks;

namespace Wabbajack.Lib.Downloaders
{
    public class VectorPlexusDownloader : AbstractIPS4Downloader<VectorPlexusDownloader, VectorPlexusDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "Vector Plexus";
        public override Uri SiteURL => new Uri("https://vectorplexus.com");
        public override Uri IconUri => new Uri("https://www.vectorplexus.com/favicon.ico");
        #endregion

        public VectorPlexusDownloader() : base(new Uri("https://vectorplexus.com/login"), 
            "vectorplexus", "vectorplexus.com")
        {
        }
        public class State : State<VectorPlexusDownloader>
        {
        }
    }
}
