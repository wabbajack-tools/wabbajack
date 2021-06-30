using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Printing;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using F23.StringSimilarity;
using Newtonsoft.Json;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Downloaders.DTOs;
using Wabbajack.Lib.Http;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public abstract class AbstractIPS4OAuthDownloader<TDownloader, TState> : INeedsLogin, IDownloader, IWaitForWindowDownloader
    where TState : AbstractIPS4OAuthDownloader<TDownloader, TState>.State, new()
    where TDownloader : IDownloader
    {
        public AbstractIPS4OAuthDownloader(string clientID, Uri authEndpoint, Uri tokenEndpoint, IEnumerable<string> scopes, string encryptedKeyName)
        {
            ClientID = clientID;
            AuthorizationEndpoint = authEndpoint;
            TokenEndpoint = tokenEndpoint;
            EncryptedKeyName = encryptedKeyName;
            Scopes = scopes;

            TriggerLogin = ReactiveCommand.CreateFromTask(
                execute: () => Utils.CatchAndLog(async () => await Utils.Log(new RequestOAuthLogin(ClientID, authEndpoint, tokenEndpoint, SiteName, scopes, EncryptedKeyName)).Task),
                canExecute: IsLoggedIn.Select(b => !b).ObserveOnGuiThread());
            ClearLogin = ReactiveCommand.CreateFromTask(
                execute: () => Utils.CatchAndLog(async () => await Utils.DeleteEncryptedJson(EncryptedKeyName)),
                canExecute: IsLoggedIn.ObserveOnGuiThread());

        }


        public IEnumerable<string> Scopes { get; }
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

        private AsyncLock _prepareLock = new();

        private bool _isPrepared = false;
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false)
        {
            if (archiveINI.General.ips4Site == SiteName && archiveINI.General.ips4Mod != null && archiveINI.General.ips4File != null)
            {
                if (!long.TryParse(archiveINI.General.ips4Mod, out long parsedMod))
                    return null;
                var state = new TState {IPS4Mod = parsedMod, IPS4File = archiveINI.General.ips4File};

                if (!quickMode)
                {
                    var downloads = await GetDownloads(state.IPS4Mod);
                    state.IPS4Url = downloads.Url ?? "";
                }

                return state;

            }

            if (archiveINI.General.ips4Site == SiteName && archiveINI.General.ips4Attachment != null)
            {
                if (!long.TryParse(archiveINI.General.ips4Attachment, out long parsedMod))
                    return null;
                var state = new TState
                {
                    IPS4Mod = parsedMod, IPS4File = archiveINI.General.ips4File, IsAttachment = true,
                    IPS4Url=$"{SiteURL}/applications/core/interface/file/attachment.php?id={parsedMod}"
                };

                return state;
            }

            return null;
        }

        public async Task<IPS4OAuthFilesResponse.Root> GetDownloads(long modID)
        {
            var url = SiteURL + $"api/downloads/files/{modID}";
            var client = await GetAuthedClient();
            using var response = await client!.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, errorsAsExceptions: false);
            string body = "";
            try
            {
                body = await response.Content.ReadAsStringAsync();
            }
            catch (Exception _)
            {
                // ignored
            }

            if (response.IsSuccessStatusCode)
                return body.FromJsonString<IPS4OAuthFilesResponse.Root>();

            Utils.Log($"IPS4 Request Error {response.StatusCode} {response.ReasonPhrase} - \n {url} \n {body}");
            throw new HttpException(response);
        }



        public async Task Prepare()
        {
            if (_isPrepared) return;
            AuthedClient = (await GetAuthedClient()) ?? throw new Exception($"Not logged into {SiteName}");
            _isPrepared = true;
        }


        private async Task<Http.Client?> GetAuthedClient()
        {
            using var _ = await _prepareLock.WaitAsync();
            if (!Utils.HaveEncryptedJson(EncryptedKeyName))
            {
                await Utils.CatchAndLog(async () => await Utils.Log(new RequestOAuthLogin(ClientID,
                    AuthorizationEndpoint, TokenEndpoint, SiteName, Scopes, EncryptedKeyName)).Task);
                if (!Utils.HaveEncryptedJson(EncryptedKeyName))
                    Utils.ErrorThrow(new Exception($"Must log into {SiteName} to continue"));
                
                return await GetAuthedClient();
            }

            var data = await Utils.FromEncryptedJson<OAuthResultState>(EncryptedKeyName);
            await data.Refresh();
            var client = new Http.Client();
            client.Headers.Add(("Authorization", $"Bearer {data.AccessToken}"));
            return client;
        }

        public async Task WaitForNextRequestWindow()
        {
            
        }
        
                
        public abstract class State : AbstractDownloadState, IMetaState
        {
            public long IPS4Mod { get; set; }
            
            public bool IsAttachment { get; set; } = false;
            public string IPS4File { get; set; } = "";
            public string IPS4Url { get; set; } = "";

            public override object[] PrimaryKey => new object[] {IPS4Mod, IPS4File ?? "", IsAttachment};

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                if (IsAttachment)
                {
                    var downloader = TypedDownloader;
                    using var driver = await WebAutomation.Driver.Create();
                    await driver.NavigateToAndDownload(
                        new Uri($"{downloader.SiteURL}/applications/core/interface/file/attachment.php?id={IPS4Mod}"), destination);
                    return true;
                }
                else
                {

                    var downloads = await TypedDownloader.GetDownloads(IPS4Mod);
                    var fileEntry = downloads.Files.First(f => f.Name == IPS4File);
                    if (a.Size != 0 && fileEntry.Size != a.Size)
                        throw new Exception(
                            $"File {IPS4File} on mod {IPS4Mod} on {TypedDownloader.SiteName} appears to be re-uploaded with the same name");

                    var state = new HTTPDownloader.State(fileEntry.Url!);
                    if (a.Size == 0) a.Size = fileEntry.Size!.Value;
                    return await state.Download(a, destination);
                }
            }

            private static AbstractIPS4OAuthDownloader<TDownloader, TState> TypedDownloader => (AbstractIPS4OAuthDownloader<TDownloader, TState>)(object)DownloadDispatcher.GetInstance<TDownloader>();

            public override async Task<bool> Verify(Archive archive, CancellationToken? token = null)
            {
                if (IsAttachment)
                {
                    var downloader = TypedDownloader;
                    using var driver = await WebAutomation.Driver.Create();
                    await using var tmp = new TempFile();
                    var foundSize = await driver.NavigateToAndDownload(
                        new Uri($"{downloader.SiteURL}/applications/core/interface/file/attachment.php?id={IPS4Mod}"), tmp.Path);
                    return archive.Size == 0 || foundSize == archive.Size;
                }
                else
                {
                    var downloads = await TypedDownloader.GetDownloads(IPS4Mod);
                    var fileEntry = downloads.Files.FirstOrDefault(f => f.Name == IPS4File);
                    if (fileEntry == null) return false;
                    return archive.Size == 0 || fileEntry.Size == archive.Size;
                }

            }

            public override string? GetManifestURL(Archive a)
            {
                return IPS4Url;
            }

            public override string[] GetMetaIni()
            {
                return new[]
                {
                    "[General]", 
                    $"ips4Site={TypedDownloader.SiteName}", 
                    $"ips4Mod={IPS4Mod}",
                    $"ips4File={IPS4File}"
                };
            }

            public Uri URL => new(IPS4Url);
            public string? Name { get; set; }
            public string? Author { get; set; }
            public string? Version { get; set; }
            public Uri? ImageURL { get; set; }
            public bool IsNSFW { get; set; }
            public string? Description { get; set; }
            public async Task<bool> LoadMetaData()
            {
                var data = await TypedDownloader.GetDownloads(IPS4Mod);
                Name = data.Title;
                Author = data.Author?.Name;
                Version = data.Version;
                ImageURL = data.PrimaryScreenshot?.Url != null ? new Uri(data.PrimaryScreenshot.Url) : null;
                IsNSFW = true;
                Description = "";
                IPS4Url = data.Url!;
                return true;
            }
            
            public async Task<List<Archive>> GetFilesInGroup()
            {
                var data = await TypedDownloader.GetDownloads(IPS4Mod);
                return data.Files.Select(f => new Archive(new TState {IPS4Mod = IPS4Mod, IPS4File = f.Name!}) {Size = f.Size!.Value}).ToList();
            }

            public override async Task<(Archive? Archive, TempFile NewFile)> FindUpgrade(Archive a, Func<Archive, Task<AbsolutePath>> downloadResolver)
            {
                var files = await GetFilesInGroup();
                var nl = new Levenshtein();

                foreach (var newFile in files.OrderBy(f => nl.Distance(a.Name.ToLowerInvariant(), f.Name.ToLowerInvariant())))
                {
                    var tmp = new TempFile();
                    await DownloadDispatcher.PrepareAll(new[] {newFile.State});
                    if (await newFile.State.Download(newFile, tmp.Path))
                    {
                        newFile.Size = tmp.Path.Size;
                        var tmpHash = await tmp.Path.FileHashAsync();
                        if (tmpHash == null) return default;
                        newFile.Hash = tmpHash.Value;
                        return (newFile, tmp);
                    }

                    await tmp.DisposeAsync();
                }
                return default;
            }
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
