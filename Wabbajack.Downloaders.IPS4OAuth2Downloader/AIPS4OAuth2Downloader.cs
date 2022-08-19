using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using F23.StringSimilarity;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.IPS4OAuth2Downloader;

public class AIPS4OAuth2Downloader<TDownloader, TLogin, TState> : ADownloader<TState>, IUpgradingDownloader
    where TLogin : OAuth2LoginState, new()
    where TState : IPS4OAuth2, new()
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly ApplicationInfo _appInfo;
    private readonly HttpClient _client;
    private readonly IHttpDownloader _downloader;
    private readonly ILogger _logger;
    private readonly ITokenProvider<TLogin> _loginInfo;
    private readonly string _siteName;
    private readonly Uri _siteURL;


    public AIPS4OAuth2Downloader(ILogger logger, ITokenProvider<TLogin> loginInfo, HttpClient client,
        IHttpDownloader downloader, ApplicationInfo appInfo, Uri siteURL, string siteName)
    {
        _logger = logger;
        _loginInfo = loginInfo;
        _client = client;
        _downloader = downloader;
        _siteURL = siteURL;
        _appInfo = appInfo;
        _siteName = siteName;
    }

    public override Priority Priority => Priority.Normal;

    public async Task<Archive?> TryGetUpgrade(Archive archive, IJob job, TemporaryFileManager temporaryFileManager,
        CancellationToken token)
    {
        var state = (TState) archive.State;
        if (state.IsAttachment) return default;

        var files = (await GetDownloads(state.IPS4Mod, token)).Files;
        var nl = new Levenshtein();

        foreach (var newFile in files.Where(f => f.Url != null)
            .OrderBy(f => nl.Distance(archive.Name.ToLowerInvariant(), f.Name!.ToLowerInvariant())))
        {
            var newArchive = new Archive
            {
                State = new TState
                {
                    IPS4Mod = state.IPS4Mod,
                    IPS4File = newFile.Name!
                }
            };
            var tmp = temporaryFileManager.CreateFile();
            var newHash = await Download(newArchive, (TState) newArchive.State, tmp.Path, job, token);
            if (newHash != default)
            {
                newArchive.Size = tmp.Path.Size();
                newArchive.Hash = newHash;
                return newArchive;
            }

            await tmp.DisposeAsync();
        }

        return default;
    }

    public async ValueTask<HttpRequestMessage> MakeMessage(HttpMethod method, Uri url, bool useOAuth2 = true)
    {
        var msg = new HttpRequestMessage(method, url);
        msg.Version = new Version(2, 0);
        var loginData = await _loginInfo.Get();
        if (useOAuth2)
        {
            msg.Headers.Add("User-Agent", _appInfo.UserAgent);
            msg.Headers.Add("Authorization", $"Bearer {loginData.ResultState.AccessToken}");
        }
        else
        {
            msg.AddCookies(loginData.Cookies)
                .AddChromeAgent();
        }

        return msg;
    }

    public async Task<IPS4OAuthFilesResponse.Root> GetDownloads(long modID, CancellationToken token)
    {
        var retried = false;
        while (true)
        {
            var url = new Uri(_siteURL + $"api/downloads/files/{modID}");
            var msg = await MakeMessage(HttpMethod.Get, url);
            using var response = await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);

            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<IPS4OAuthFilesResponse.Root>(SerializerOptions, token))
                    !;

            if (retried)
            {
                _logger.LogCritical("IPS4 Request Error {response} {reason} - \n {url}", response.StatusCode,
                    response.ReasonPhrase, url);
                throw new HttpException(response);
            }

            if (!await SimpleTokenRenew(token))
            {
                _logger.LogCritical("IPS4 Request Error and couldn't renew {response} {reason} - \n {url}",
                    response.StatusCode, response.ReasonPhrase, url);
                throw new HttpException(response);
            }

            retried = true;
        }
    }

    public override async Task<Hash> Download(Archive archive, TState state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        if (state.IsAttachment)
        {
            var msg = await MakeMessage(HttpMethod.Get,
                new Uri($"{_siteURL}/applications/core/interface/file/attachment.php?id={state.IPS4Mod}"), false);
            return await _downloader.Download(msg, destination, job, token);
        }
        else
        {
            var downloads = await GetDownloads(state.IPS4Mod, token);
            var fileEntry = downloads.Files.FirstOrDefault(f => f.Name == state.IPS4File);
            var msg = new HttpRequestMessage(HttpMethod.Get, fileEntry.Url);
            msg.Version = new Version(2, 0);
            msg.Headers.Add("User-Agent", _appInfo.UserAgent);
            return await _downloader.Download(msg, destination, job, token);
        }
    }

    public override async Task<bool> Prepare()
    {
        return _loginInfo.HaveToken();
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    private async Task<bool> SimpleTokenRenew(CancellationToken token)
    {
        var tLogin = new TLogin();

        var scopes = string.Join(" ", tLogin.Scopes);
        var state = Guid.NewGuid().ToString();

        var authMessage = await MakeMessage(HttpMethod.Get, new Uri(tLogin.AuthorizationEndpoint +
                                                                    $"?response_type=code&client_id={tLogin.ClientID}&state={state}&scope={scopes}"),
            false);
        using var authResponse = await _client.SendAsync(authMessage, HttpCompletionOption.ResponseHeadersRead, token);

        if (authResponse.StatusCode != HttpStatusCode.Redirect)
        {
            _logger.LogCritical("Quick renew auth returned {code} - {message} - {body}", authResponse.StatusCode,
                authResponse.ReasonPhrase, await authResponse.Content.ReadAsStringAsync());
            return false;
        }

        var redirect = authResponse.Headers.GetValues("Location").FirstOrDefault();
        if (redirect == default) return false;

        var parsed = HttpUtility.ParseQueryString(new Uri(redirect!).Query);
        if (parsed.Get("state") != state)
        {
            _logger.LogCritical("Bad OAuth state, this shouldn't happen");
            throw new Exception("Bad OAuth State");
        }

        if (parsed.Get("code") == null)
        {
            _logger.LogCritical("Bad code result from OAuth");
            throw new Exception("Bad code result from OAuth");
        }

        var authCode = parsed.Get("code");

        var formData = new KeyValuePair<string?, string?>[]
        {
            new("grant_type", "authorization_code"),
            new("code", authCode),
            new("client_id", tLogin.ClientID)
        };
        var msg = await MakeMessage(HttpMethod.Post, tLogin.TokenEndpoint, false);

        msg.Content = new FormUrlEncodedContent(formData.ToList());

        using var response = await _client.SendAsync(msg, token);
        var data = await response.Content.ReadFromJsonAsync<OAuthResultState>(cancellationToken: token);

        var prevData = await _loginInfo.Get() ?? new TLogin();
        prevData.ResultState = data!;
        await _loginInfo.SetToken(prevData);

        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (!iniData.ContainsKey("ips4Site") || iniData["ips4Site"] != _siteName) return null;

        if (iniData.ContainsKey("ips4Mod") && iniData.ContainsKey("ips4File"))
        {
            if (!long.TryParse(iniData["ips4Mod"], out var parsedMod))
                return null;
            var state = new TState {IPS4Mod = parsedMod, IPS4File = iniData["ips4File"]};
            return state;
        }

        if (iniData.ContainsKey("ips4Attachment") != default)
        {
            if (!long.TryParse(iniData["ips4Attachment"], out var parsedMod))
                return null;
            var state = new TState
            {
                IPS4Mod = parsedMod,
                IsAttachment = true,
                IPS4Url = $"{_siteURL}/applications/core/interface/file/attachment.php?id={parsedMod}"
            };

            return state;
        }

        return null;
    }

    public override async Task<bool> Verify(Archive archive, TState state, IJob job, CancellationToken token)
    {
        if (state.IsAttachment)
        {
            var msg = await MakeMessage(HttpMethod.Get,
                new Uri($"{_siteURL}/applications/core/interface/file/attachment.php?id={state.IPS4Mod}"), false);
            using var response = await _client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
            return response.IsSuccessStatusCode;
        }

        var downloads = await GetDownloads(state.IPS4Mod, token);
        var fileEntry = downloads.Files.FirstOrDefault(f => f.Name == state.IPS4File);
        if (fileEntry == null) return false;
        return archive.Size == 0 || fileEntry.Size == archive.Size;
    }

    public override IEnumerable<string> MetaIni(Archive a, TState state)
    {
        return new[]
        {
            $"ips4Site={_siteName}",
            $"ips4Mod={state.IPS4Mod}",
            $"ips4File={state.IPS4File}"
        };
    }
}