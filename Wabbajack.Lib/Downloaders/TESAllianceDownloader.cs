using System;
using MessagePack;

namespace Wabbajack.Lib.Downloaders
{
    public class TESAllianceDownloader : AbstractIPS4Downloader<TESAllianceDownloader, TESAllianceDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "TESAlliance";
        public override Uri SiteURL => new Uri("http://tesalliance.org/forums/index.php?");
        public override Uri IconUri => new Uri("http://tesalliance.org/favicon.ico");
        #endregion

        public TESAllianceDownloader() : base(new Uri("http://tesalliance.org/forums/index.php?/login/"),
            "tesalliance", "tesalliance.org")
        {
        }

        [MessagePackObject]
        public class State : State<TESAllianceDownloader>{}
    }
}
