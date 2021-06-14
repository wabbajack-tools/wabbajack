using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Http;

namespace Wabbajack.Lib.Downloaders
{
    public abstract class AbstractIPS4OAuthDownloader<TDownloader, TState> : INeedsLogin, IDownloader, IWaitForWindowDownloader
    where TState : AbstractIPS4OAuthDownloader<TDownloader, TState>.State, new()
    where TDownloader : IDownloader
    {
        public AbstractIPS4OAuthDownloader(string clientID, Uri authEndpoint, Uri tokenEndpoint, string encryptedKeyName)
        {
            ClientID = clientID;
            AuthorizationEndpoint = authEndpoint;
            TokenEndpoint = tokenEndpoint;
            EncryptedKeyName = encryptedKeyName;

            TriggerLogin = ReactiveCommand.CreateFromTask(
                execute: () => Utils.CatchAndLog(async () => await Utils.Log(new RequestOAuthLogin(ClientID, authEndpoint, tokenEndpoint, SiteName, new []{"profile"}, EncryptedKeyName)).Task),
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
        public Client? AuthedClient { get; set; }

        private bool _isPrepared = false;
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false)
        {
            if (archiveINI.General != null && archiveINI.General.directURL != null)
            {
                var parsed = new Uri(archiveINI.General.directURL);
                var fileID = parsed.AbsolutePath.Split("/").Last().Split("-").First();
                var modID = HttpUtility.ParseQueryString(parsed.Query).Get("r");

                if (!long.TryParse(fileID, out var fileIDParsed))
                    return null;
                if (modID != null && !long.TryParse(modID, out var modIDParsed))
                    return null;
            }

            throw new NotImplementedException();
        }

        public async Task Prepare()
        {

            if (_isPrepared) return;
            AuthedClient = (await GetAuthedClient()) ?? throw new Exception($"Not logged into {SiteName}");
            _isPrepared = true;
        }


        private async Task<Http.Client?> GetAuthedClient()
        {
            if (!Utils.HaveEncryptedJson(EncryptedKeyName))
                return null;

            var data = await Utils.FromEncryptedJson<OAuthResultState>(EncryptedKeyName);
            await data.Refresh();
            var client = new Http.Client();
            client.Headers.Add(("Authorization", $"Bearer {data.AccessToken}"));
            return client;
        }

        public async Task WaitForNextRequestWindow()
        {
            
        }
        
                
        public abstract class State : AbstractDownloadState
        {
            public long FileID { get; set; }
            public long? ModID { get; set; }
        }
    }

    [JsonName("OAuthResultState")]
    public class OAuthResultState
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = "";
        
        [JsonProperty("token_type")]
        public string TokenType { get; set; } = "";
        
        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }
        
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = "";
        
        [JsonProperty("scope")]
        public string Scope { get; set; } = "";
        
        [JsonProperty("authorization_code")]
        public string AuthorizationCode { get; set; } = "";
        
        [JsonProperty("token_endpoint")]
        public Uri? TokenEndpoint { get; set; }
        
        [JsonProperty("expires_at")]
        public DateTime ExpiresAt { get; set; }
        
        [JsonProperty("client_id")]
        public string ClientID { get; set; } = "";

        internal void FillInData(string authCode, RequestOAuthLogin oa)
        {
            AuthorizationCode = authCode;
            TokenEndpoint = oa.TokenEndpoint;
            ExpiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(ExpiresIn);
            ClientID = oa.ClientID;
        }


        public async Task<bool> Refresh()
        {
            if (ExpiresAt > DateTime.UtcNow + TimeSpan.FromHours(6))
                return true;
            
            var client = new Http.Client();
            var formData = new KeyValuePair<string?, string?>[]
            {
                new ("grant_type", "refresh_token"), 
                new ("refresh_token", RefreshToken),
                new ("client_id", ClientID)
            };
            using var response = await client.PostAsync(TokenEndpoint!.ToString(), new FormUrlEncodedContent(formData.ToList()));
            var responseData = (await response.Content.ReadAsStringAsync()).FromJsonString<OAuthResultState>();

            AccessToken = responseData.AccessToken;
            ExpiresIn = responseData.ExpiresIn;
            ExpiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(ExpiresIn);
            
            return true;
        }


    }
    
    public class RequestOAuthLogin : AUserIntervention
    {
        public string ClientID { get; }
        public Uri AuthorizationEndpoint { get; }
        public Uri TokenEndpoint { get; }
        public string SiteName { get; }
        
        public string[] Scopes { get; }
            
        public RequestOAuthLogin(string clientID, Uri authEndpoint, Uri tokenEndpoint, string siteName, IEnumerable<string> scopes, string key)
        {
            ClientID = clientID;
            AuthorizationEndpoint = authEndpoint;
            TokenEndpoint = tokenEndpoint;
            SiteName = siteName;
            Scopes = scopes.ToArray();
            EncryptedDataKey = key;
        }

        public string EncryptedDataKey { get; set; }

        public override string ShortDescription => $"Getting {SiteName} Login";
        public override string ExtendedDescription { get; } = string.Empty;

        private readonly TaskCompletionSource<OAuthResultState> _source = new ();
        public Task<OAuthResultState> Task => _source.Task;

        public async Task Resume(string authCode)
        {
            Handled = true;

            var client = new Http.Client();
            var formData = new KeyValuePair<string?, string?>[]
            {
                new ("grant_type", "authorization_code"), 
                new ("code", authCode), 
                new ("client_id", ClientID)
            };
            using var response = await client.PostAsync(TokenEndpoint.ToString(), new FormUrlEncodedContent(formData.ToList()));
            var responseData = (await response.Content.ReadAsStringAsync()).FromJsonString<OAuthResultState>();
            responseData.FillInData(authCode, this);

            await responseData.ToEcryptedJson(EncryptedDataKey);
            _source.SetResult(new OAuthResultState());
        }

        public override void Cancel()
        {
            Handled = true;
            _source.TrySetCanceled();
        }


    }
}
