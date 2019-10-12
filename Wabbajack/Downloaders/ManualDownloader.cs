using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Validation;

namespace Wabbajack.Downloaders
{
    class ManualDownloader : IDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.manualURL;
            return url != null ? new State { Url = url} : null;
        }

        public void Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override void Download(Archive a, string destination)
            {
                Utils.Log($"You must manually visit {Url} and download {a.Name} file by hand.");
            }

            public override bool Verify()
            {
                return true;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<ManualDownloader>();
            }
        }
    }
}
