using System;

namespace Wabbajack.Lib.Downloaders
{
    public class DeadlyStreamDownloader : AbstractIPS4Downloader<DeadlyStreamDownloader, DeadlyStreamDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "Deadly Stream";
        public override Uri SiteURL => new Uri("https://www.deadlystream.com");
        public override Uri IconUri => new Uri("https://www.deadlystream.com/favicon.ico");
        #endregion

        public DeadlyStreamDownloader() : base(new Uri("https://deadlystream.com/login"), "deadlystream",
            "deadlystream.com")
        {

        }

        public class State : State<DeadlyStreamDownloader>
        {

        }
    }
}
