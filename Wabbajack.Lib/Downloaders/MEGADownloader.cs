using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Security;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using Newtonsoft.Json;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib.Downloaders
{
    public class MegaDownloader : IUrlDownloader, INeedsLoginCredentials
    {
        public MegaApiClient MegaApiClient;
        private const string DataName = "mega-cred";

        [JsonName("MEGAAuthInfos")]
        //https://github.com/gpailler/MegaApiClient/blob/master/MegaApiClient/MegaApiClient.cs#L1242
        internal class MEGAAuthInfos
        {
            [JsonProperty] 
            public string Email { get; private set; } = string.Empty;

            [JsonProperty]
            public string Hash { get; private set; } = string.Empty;

            [JsonProperty]
            public byte[]? PasswordAesKey { get; private set; }

            [JsonProperty]
            public string MFAKey { get; private set; } = string.Empty;

            public static MEGAAuthInfos ToMEGAAuthInfos(MegaApiClient.AuthInfos infos)
            {
                return new MEGAAuthInfos
                {
                    Email = infos.Email,
                    Hash = infos.Hash,
                    PasswordAesKey = infos.PasswordAesKey,
                    MFAKey = infos.MFAKey
                };
            }

            public MegaApiClient.AuthInfos ToAuthInfos()
            {
                return new MegaApiClient.AuthInfos(Email, Hash, PasswordAesKey, MFAKey);
            }
        }

        public async Task<LoginReturnMessage> LoginWithCredentials(string username, SecureString password, string? mfa)
        {
            MegaApiClient.AuthInfos authInfos;

            try
            {
                MegaApiClient.Logout();
            }
            catch (NotSupportedException)
            {
                // Not logged in, so ignore
            }

            try
            {
                authInfos = MegaApiClient.GenerateAuthInfos(username, password.ToNormalString(), mfa);
            }
            catch (ApiException e)
            {
                return e.ApiResultCode switch
                {
                    ApiResultCode.BadArguments => new LoginReturnMessage($"Email or password was wrong! {e.Message}",
                        LoginReturnCode.BadCredentials),
                    ApiResultCode.InternalError => new LoginReturnMessage(
                        $"Internal error occured! Please report this to the Wabbajack Team! {e.Message}", LoginReturnCode.InternalError),
                    _ => new LoginReturnMessage($"Error generating authentication information! {e.Message}", LoginReturnCode.InternalError)
                };
            }
            
            try
            {
                MegaApiClient.Login(authInfos);
            }
            catch (ApiException e)
            {
                return e.ApiResultCode switch
                {
                    ApiResultCode.TwoFactorAuthenticationError => new LoginReturnMessage(
                        $"Two-Factor Authentication is enabled. Input your TFA key above and try again! {e.Message}", LoginReturnCode.NeedsMFA),
                    ApiResultCode.InternalError => new LoginReturnMessage(
                        $"Internal error occured! Please report this to the Wabbajack Team! {e.Message}", LoginReturnCode.InternalError),
                    ApiResultCode.RequestIncomplete => new LoginReturnMessage(
                        $"Bad credentials! {e.Message}", LoginReturnCode.BadCredentials),
                    ApiResultCode.ResourceNotExists => new LoginReturnMessage(
                        $"Bad credentials! {e.Message}", LoginReturnCode.BadCredentials),
                    _ => new LoginReturnMessage($"Error during login: {e.Message}", LoginReturnCode.InternalError)
                };
            }

            if (MegaApiClient.IsLoggedIn)
            {
                var infos = MEGAAuthInfos.ToMEGAAuthInfos(authInfos);
                await infos.ToEcryptedJson(DataName);
            }

            return new LoginReturnMessage("Logged in successfully, you can now close this window.",
                !MegaApiClient.IsLoggedIn || !Utils.HaveEncryptedJson(DataName) ? LoginReturnCode.Success : LoginReturnCode.InternalError);
        }

        public MegaDownloader()
        {
            MegaApiClient = new MegaApiClient();

            TriggerLogin = ReactiveCommand.Create(() => { },
                IsLoggedIn.Select(b => !b).ObserveOnGuiThread());

            ClearLogin = ReactiveCommand.CreateFromTask(() => Utils.CatchAndLog(async () => await Utils.DeleteEncryptedJson(DataName)),
                IsLoggedIn.ObserveOnGuiThread());
        }

        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState? GetDownloaderState(string url)
        {
            if (url != null && (url.StartsWith(Consts.MegaPrefix) || url.StartsWith(Consts.MegaFilePrefix)))
                return new State(url);
            return null;
        }

        public async Task Prepare()
        {
        }

        [JsonName("MegaDownloader")]
        public class State : HTTPDownloader.State
        {
            public State(string url) 
                : base(url)
            {
            }

            private static MegaApiClient MegaApiClient => DownloadDispatcher.GetInstance<MegaDownloader>().MegaApiClient;

            private static readonly AsyncLock _loginLock = new AsyncLock();
            private static async Task MegaLogin()
            {
                using var _ = await _loginLock.WaitAsync();

                if (MegaApiClient.IsLoggedIn)
                    return;

                if (!Utils.HaveEncryptedJson(DataName))
                {
                    Utils.Status("Logging into MEGA (as anonymous)");
                    await MegaApiClient.LoginAnonymousAsync();
                }
                else
                {
                    Utils.Status("Logging into MEGA with saved credentials.");
                    var infos = await Utils.FromEncryptedJson<MEGAAuthInfos>(DataName);
                    var authInfo = infos.ToAuthInfos();
                    await MegaApiClient.LoginAsync(authInfo);
                }
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                await MegaLogin();

                var fileLink = new Uri(Url);
                Utils.Status($"Downloading MEGA file: {a.Name}");
                await MegaApiClient.DownloadFileAsync(fileLink, (string)destination, new Progress<double>(p => Utils.Status($"Downloading MEGA File: {a.Name}", Percent.FactoryPutInRange(p))));
                return true;
            }

            public override async Task<bool> Verify(Archive a)
            {
                await MegaLogin();

                var fileLink = new Uri(Url);
                try
                {
                    var node = await MegaApiClient.GetNodeFromLinkAsync(fileLink);
                    return node != null;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public override Task<(Archive? Archive, TempFile NewFile)> FindUpgrade(Archive a, Func<Archive, Task<AbsolutePath>> downloadResolver)
            {
                return ServerFindUpgrade(a);
            }
            
            public override async Task<bool> ValidateUpgrade(Hash srcHash, AbstractDownloadState newArchiveState)
            {
                return await ServerValidateUpgrade(srcHash, newArchiveState);
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
