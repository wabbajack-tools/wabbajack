using System;
using CG.Web.MegaApiClient;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public class MegaDownloader : IDownloader, IUrlDownloader
    {

        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url != null && url.StartsWith(Consts.MegaPrefix))
                return new State { Url = url };
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
