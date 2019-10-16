using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CG.Web.MegaApiClient;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class MegaDownloader : IDownloader
    {

        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.directURL;
            if (url != null && url.StartsWith(Consts.MegaPrefix))
                return new State {Url = url};
            return null;
        }

        public void Prepare()
        {
        }

        public class State : HTTPDownloader.State
        {
            public override void Download(Archive a, string destination)
            {
                var client = new MegaApiClient();
                Utils.Status("Logging into MEGA (as anonymous)");
                client.LoginAnonymous();
                var file_link = new Uri(Url);
                var node = client.GetNodeFromLink(file_link);
                Utils.Status($"Downloading MEGA file: {a.Name}");
                client.DownloadFile(file_link, destination);
            }

            public override bool Verify()
            {
                var client = new MegaApiClient();
                Utils.Status("Logging into MEGA (as anonymous)");
                client.LoginAnonymous();
                var file_link = new Uri(Url);
                try
                {
                    var node = client.GetNodeFromLink(file_link);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
