using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using ReactiveUI;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public class MegaDownloader : IUrlDownloader, INeedsLogin
    {
        public MegaApiClient MegaApiClient;
        private const string DataName = "mega-cred";

        public MegaDownloader()
        {
            MegaApiClient = new MegaApiClient();

            TriggerLogin = ReactiveCommand.Create(() => { },
                IsLoggedIn.Select(b => !b).ObserveOnGuiThread());

            ClearLogin = ReactiveCommand.Create(() => Utils.CatchAndLog(() => Utils.DeleteEncryptedJson(DataName)),
                IsLoggedIn.ObserveOnGuiThread());
        }

        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url != null && url.StartsWith(Consts.MegaPrefix))
                return new State { Url = url, MegaApiClient = MegaApiClient};
            return null;
        }

        public async Task Prepare()
        {
        }

        public class State : HTTPDownloader.State
        {
            public MegaApiClient MegaApiClient;

            public override async Task<bool> Download(Archive a, string destination)
            {
                if (!MegaApiClient.IsLoggedIn)
                {
                    Utils.Status("Logging into MEGA (as anonymous)");
                    MegaApiClient.LoginAnonymous();
                }

                var fileLink = new Uri(Url);
                var node = MegaApiClient.GetNodeFromLink(fileLink);
                Utils.Status($"Downloading MEGA file: {a.Name}");
                MegaApiClient.DownloadFile(fileLink, destination);
                return true;
            }

            public override async Task<bool> Verify(Archive a)
            {
                if (!MegaApiClient.IsLoggedIn)
                {
                    Utils.Status("Logging into MEGA (as anonymous)");
                    MegaApiClient.LoginAnonymous();
                }

                var fileLink = new Uri(Url);
                try
                {
                    var node = MegaApiClient.GetNodeFromLink(fileLink);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public ReactiveCommand<Unit, Unit> TriggerLogin { get; }
        public ReactiveCommand<Unit, Unit> ClearLogin { get; }
        public IObservable<bool> IsLoggedIn => Utils.HaveEncryptedJsonObservable(DataName);
        public string SiteName => "MEGA";
        public IObservable<string> MetaInfo => Observable.Return("");
        public Uri SiteURL => new Uri("https://mega.nz/");
        public Uri IconUri => new Uri("https://mega.nz/favicon.ico");
    }
}
