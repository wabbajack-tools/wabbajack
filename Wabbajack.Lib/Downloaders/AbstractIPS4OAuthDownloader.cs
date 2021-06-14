using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Org.BouncyCastle.Crypto.Parameters;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.WebAutomation;
namespace Wabbajack.Lib.Downloaders
{
    public abstract class AbstractIPS4OAuthDownloader<TDownloader, TState> : INeedsLogin, IDownloader, IWaitForWindowDownloader
    where TState : AbstractDownloadState, new()
    where TDownloader : IDownloader
    {
        public AbstractIPS4OAuthDownloader(string clientID, Uri authEndpoint, Uri tokenEndpoint, string encryptedKeyName)
        {
            ClientID = clientID;
            AuthorizationEndpoint = authEndpoint;
            TokenEndpoint = tokenEndpoint;
            EncryptedKeyName = encryptedKeyName;

            TriggerLogin = ReactiveCommand.CreateFromTask(
                execute: () => Utils.CatchAndLog(async () => await Utils.Log(new RequestOAuthLogin(ClientID, authEndpoint, tokenEndpoint, SiteName)).Task),
                canExecute: IsLoggedIn.Select(b => !b).ObserveOnGuiThread());
            ClearLogin = ReactiveCommand.CreateFromTask(
                execute: () => Utils.CatchAndLog(async () => await Utils.DeleteEncryptedJson(EncryptedKeyName)),
                canExecute: IsLoggedIn.ObserveOnGuiThread());

        }

        public string EncryptedKeyName { get; }
        public Uri TokenEndpoint { get; }
        public Uri AuthorizationEndpoint { get; }
        public string ClientID { get; }
        public ReactiveCommand<Unit, Unit> TriggerLogin { get; }
        public ReactiveCommand<Unit, Unit> ClearLogin { get; }
        public IObservable<bool> IsLoggedIn => Utils.HaveEncryptedJsonObservable(EncryptedKeyName);
        public abstract string SiteName { get; }
        public IObservable<string>? MetaInfo { get; }
        public abstract Uri SiteURL { get; }
        public virtual Uri? IconUri { get; }
        public Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false)
        {
            throw new NotImplementedException();
        }

        public Task Prepare()
        {
            throw new NotImplementedException();
        }

        public Task WaitForNextRequestWindow()
        {
            throw new NotImplementedException();
        }
    }
    
    public class RequestOAuthLogin : AUserIntervention
    {
        public string ClientID { get; }
        public Uri AuthorizationEndpoint { get; }
        public Uri TokenEndpoint { get; }
        public string SiteName { get; }
            
        public RequestOAuthLogin(string clientID, Uri authEndpoint, Uri tokenEndpoint, string siteName)
        {
            ClientID = clientID;
            AuthorizationEndpoint = authEndpoint;
            TokenEndpoint = tokenEndpoint;
            SiteName = siteName;
        }
        public override string ShortDescription => $"Getting {SiteName} Login";
        public override string ExtendedDescription { get; } = string.Empty;

        private readonly TaskCompletionSource<string> _source = new ();
        public Task<string> Task => _source.Task;

        public void Resume(string authToken)
        {
            Handled = true;
            _source.SetResult(authToken);
        }

        public override void Cancel()
        {
            Handled = true;
            _source.TrySetCanceled();
        }
    }
}
