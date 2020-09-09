using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.Downloaders
{
    public abstract class AbstractNeedsLoginDownloader : INeedsLogin
    {
        private readonly Uri _loginUri;
        private readonly string _encryptedKeyName;
        private readonly string _cookieDomain;
        private readonly string _cookieName;

        private bool _isPrepared;

        // ToDo
        // Remove null assignment.  Either add nullability to type, or figure way to prepare it safely
        public Wabbajack.Lib.Http.Client AuthedClient { get; private set; } = null!;

        public ReactiveCommand<Unit, Unit> TriggerLogin { get; }
        public ReactiveCommand<Unit, Unit> ClearLogin { get; }
        public IObservable<bool> IsLoggedIn => Utils.HaveEncryptedJsonObservable(_encryptedKeyName);
        public abstract string SiteName { get; }
        public virtual IObservable<string>? MetaInfo { get; }
        public abstract Uri SiteURL { get; }
        public virtual Uri? IconUri { get; }

        /// <summary>
        /// Sets up all the login facilites needed for a INeedsLogin downloader based on having the user log
        /// in via a browser
        /// </summary>
        /// <param name="loginUri">The URI to preset for logging in</param>
        /// <param name="encryptedKeyName">The name of the encrypted JSON key in which to store cookies</param>
        /// <param name="cookieDomain">The cookie domain to scan</param>
        /// <param name="cookieName">The cookie name to wait for</param>
        public AbstractNeedsLoginDownloader(Uri loginUri, 
            string encryptedKeyName, 
            string cookieDomain,
            string cookieName)
        {
            _loginUri = loginUri;
            _encryptedKeyName = encryptedKeyName;
            _cookieDomain = cookieDomain;
            _cookieName = cookieName;
            
            TriggerLogin = ReactiveCommand.CreateFromTask(
                execute: () => Utils.CatchAndLog(async () => await Utils.Log(new RequestSiteLogin(this)).Task),
                canExecute: IsLoggedIn.Select(b => !b).ObserveOnGuiThread());
            ClearLogin = ReactiveCommand.CreateFromTask(
                execute: () => Utils.CatchAndLog(async () => await Utils.DeleteEncryptedJson(_encryptedKeyName)),
                canExecute: IsLoggedIn.ObserveOnGuiThread());
        }

        protected virtual async Task WhileWaiting(IWebDriver browser)
        {
        }
        
        public async Task<Helpers.Cookie[]> GetAndCacheCookies(IWebDriver browser, Action<string> updateStatus, CancellationToken cancel)
        {
            updateStatus($"Please Log Into {SiteName}");
            await browser.NavigateTo(_loginUri);
            var cookies = new Helpers.Cookie[0];
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                await WhileWaiting(browser);
                cookies = (await browser.GetCookies(_cookieDomain));
                if (cookies.FirstOrDefault(c => c.Name == _cookieName) != null)
                    break;
                await Task.Delay(500, cancel);
            }

            await cookies.ToEcryptedJson(_encryptedKeyName);

            return cookies;
        }
        
        public async Task<Http.Client> GetAuthedClient()
        {
            Helpers.Cookie[] cookies;
            if (Consts.IsServer)
            {
                try
                {
                    cookies = await Utils.FromEncryptedJson<Helpers.Cookie[]>(_encryptedKeyName);
                    if (cookies != null)
                        return Helpers.GetClient(cookies, SiteURL.ToString());
                }
                catch (FileNotFoundException) { }

                cookies = await Utils.Log(new RequestSiteLogin(this)).Task;
                return Helpers.GetClient(cookies, SiteURL.ToString());
            }

            cookies = await ClientAPI.GetAuthInfo<Helpers.Cookie[]>(_encryptedKeyName);
            return Helpers.GetClient(cookies, SiteURL.ToString());
        }
        
        public async Task Prepare()
        {
            if (_isPrepared) return;
            AuthedClient = (await GetAuthedClient()) ?? throw new NotLoggedInError(this);
            _isPrepared = true;
        }

        public class NotLoggedInError : Exception
        {
            public AbstractNeedsLoginDownloader Downloader { get; }
            public NotLoggedInError(AbstractNeedsLoginDownloader downloader) : base(
                $"Not logged into {downloader.SiteName}, can't continue")
            {
                Downloader = downloader;
            }
        }

        
        public class RequestSiteLogin : AUserIntervention
        {
            public AbstractNeedsLoginDownloader Downloader { get; }
            public RequestSiteLogin(AbstractNeedsLoginDownloader downloader)
            {
                Downloader = downloader;
            }
            public override string ShortDescription => $"Getting {Downloader.SiteName} Login";
            public override string ExtendedDescription { get; } = string.Empty;

            private readonly TaskCompletionSource<Helpers.Cookie[]> _source = new TaskCompletionSource<Helpers.Cookie[]>();
            public Task<Helpers.Cookie[]> Task => _source.Task;

            public void Resume(Helpers.Cookie[] cookies)
            {
                Handled = true;
                _source.SetResult(cookies);
            }

            public override void Cancel()
            {
                Handled = true;
                _source.TrySetCanceled();
            }
        }
    }
}
