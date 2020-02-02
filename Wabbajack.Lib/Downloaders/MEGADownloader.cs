using System;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public class MegaDownloader : IDownloader, IUrlDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url != null && url.StartsWith(Consts.MegaPrefix))
                return new State { Url = url };
            return null;
        }

        public async Task Prepare()
        {
        }

        public class State : HTTPDownloader.State
        {
            public override async Task<bool> Download(Archive a, string destination)
            {
                var client = new MegaApiClient();
                Utils.Status("Logging into MEGA (as anonymous)");
                client.LoginAnonymous();
                var fileLink = new Uri(Url);
                var node = client.GetNodeFromLink(fileLink);
                Utils.Status($"Downloading MEGA file: {a.Name}");
                client.DownloadFile(fileLink, destination);
                return true;
            }

            public override async Task<bool> Verify(Archive a)
            {
                var client = new MegaApiClient();
                Utils.Status("Logging into MEGA (as anonymous)");
                client.LoginAnonymous();
                var fileLink = new Uri(Url);
                try
                {
                    var node = client.GetNodeFromLink(fileLink);
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
