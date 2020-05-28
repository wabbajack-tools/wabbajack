using System;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib.Downloaders
{
    public class TESAllDownloader : AbstractIPS4Downloader<TESAllDownloader, TESAllDownloader.State>
    {
        #region INeedsDownload
        public override string SiteName => "TESALL";
        public override Uri SiteURL => new Uri("http://tesall.ru");
        public override Uri IconUri => new Uri("http://tesall.ru/favicon.ico");
        #endregion

        public TESAllDownloader() : base(new Uri("https://tesall.ru/index.php?app=core&module=global&section=login"),
            "tesall", "tesall.ru", "member_id")
        {
        }

        [JsonName("TESAllDownloader")]
        public class State : State<TESAllDownloader>{}
    }
}
