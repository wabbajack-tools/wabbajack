using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Security;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using MessagePack;
using ReactiveUI;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public class MegaDownloader : IUrlDownloader, INeedsLoginCredentials
    {
        public MegaApiClient MegaApiClient;
        private const string DataName = "mega-cred";

        public LoginReturnMessage LoginWithCredentials(string username, SecureString password)
        {
            MegaApiClient.AuthInfos authInfos;

            try
            {
                authInfos = MegaApiClient.GenerateAuthInfos(username, password.ToNormalString());
                username = null;
                password = null;
            }
            catch (ApiException e)
            {
                return e.ApiResultCode switch
                {
                    ApiResultCode.BadArguments => new LoginReturnMessage($"Email or password was wrong! {e.Message}",
                        true),
                    ApiResultCode.InternalError => new LoginReturnMessage(
                        $"Internal error occured! Please report this to the Wabbajack Team! {e.Message}", true),
                    _ => new LoginReturnMessage($"Error generating authentication information! {e.Message}", true)
                };
            }
            
            try
            {
                MegaApiClient.Login(authInfos);
            }
            catch (ApiException e)
            {
                if ((int)e.ApiResultCode == -26)
                {
                    return new LoginReturnMessage("Two-Factor Authentication needs to be disabled before login!", true);
                }
                return e.ApiResultCode switch
                {
                    ApiResultCode.InternalError => new LoginReturnMessage(
                        $"Internal error occured! Please report this to the Wabbajack Team! {e.Message}", true),
                    _ => new LoginReturnMessage($"Error during login: {e.Message}", true)
                };
            }

            if(MegaApiClient.IsLoggedIn)
                authInfos.ToEcryptedJson(DataName);

            return new LoginReturnMessage("Logged in successfully, you can now close this window.",
                !MegaApiClient.IsLoggedIn || !Utils.HaveEncryptedJson(DataName));
        }

        public MegaDownloader()
        {
            MegaApiClient = new MegaApiClient();

            TriggerLogin = ReactiveCommand.Create(() => { },
                IsLoggedIn.Select(b => !b).ObserveOnGuiThread());

            ClearLogin = ReactiveCommand.Create(() => Utils.CatchAndLog(() => Utils.DeleteEncryptedJson(DataName)),
                IsLoggedIn.ObserveOnGuiThread());
        }

        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI, bool quickMode)
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
            [IgnoreMember]
            internal MegaApiClient MegaApiClient;

            private void MegaLogin()
            {
                if (MegaApiClient.IsLoggedIn)
                    return;

                if (!Utils.HaveEncryptedJson(DataName))
                {
                    Utils.Status("Logging into MEGA (as anonymous)");
                    MegaApiClient.LoginAnonymous();
                }
                else
                {
                    Utils.Status("Logging into MEGA with saved credentials.");
                    var authInfo = Utils.FromEncryptedJson<MegaApiClient.AuthInfos>(DataName);
                    MegaApiClient.Login(authInfo);
                }
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                MegaLogin();

                var fileLink = new Uri(Url);
                Utils.Status($"Downloading MEGA file: {a.Name}");
                await MegaApiClient.DownloadFileAsync(fileLink, (string)destination, new Progress<double>(p => Utils.Status($"Downloading MEGA File: {a.Name}", Percent.FactoryPutInRange(p))));
                return true;
            }

            public override async Task<bool> Verify(Archive a)
            {
                MegaLogin();

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
