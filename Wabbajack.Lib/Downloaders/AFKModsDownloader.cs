using System;

namespace Wabbajack.Lib.Downloaders
{ 
    public class AFKModsDownloader : AbstractIPS4Downloader<AFKModsDownloader, AFKModsDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "AFK Mods";
        public override Uri SiteURL => new Uri("https://www.afkmods.com");
        public override Uri IconUri => new Uri("https://www.afkmods.com/favicon.ico");
        #endregion

        public AFKModsDownloader() : base(new Uri("https://www.afkmods.com/index.php?/login/"),
            "afkmods", "www.afkmods.com"){}

        public class State : State<AFKModsDownloader>{}
    }
}
