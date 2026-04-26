using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.Logins.BethesdaNet;
using Wabbajack.Networking.BethesdaNet.DTOs;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.BethesdaNet;



/// <summary>
/// This code is heavily based on code researched and prototyped by Nukem9 https://github.com/Nukem9/bethnet_cli
/// </summary>
public class Client
{
    private readonly ITokenProvider<BethesdaNetLoginState> _tokenProvider;
    private readonly ILogger<Client> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private CDPAuthResponse? _entitlementData;
    private const string AgentPlatform = "WINDOWS";
    private const string AgentProduct = "FALLOUT4";
    private const string AgentLanguage = "en";
    private const string ClientApiKey = "FeBqmQA8wxd94RtqymKwzmtcQcaA5KHOpDkQBSegx4WePeluZTCIm5scoeKTbmGl";
    //private const string ClientId = "95578d65-45bf-4a03-b7f7-a43d29b9467d";
    private const string AgentVersion = $"{AgentProduct};;BDK;1.0013.99999.1;{AgentPlatform}";
    private string FingerprintKey { get; set; }


    public Client(ILogger<Client> logger, HttpClient client, ITokenProvider<BethesdaNetLoginState> tokenProvider)
    {
        _tokenProvider = tokenProvider;
        _logger = logger;
        _httpClient = client;
        _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
        SetFingerprint();
    }

    public async Task Login(CancellationToken token)
    {
        _logger.LogInformation("Logging into BethesdaNet");
        var loginData = await _tokenProvider.Get();
        var msg = MakeMessage(HttpMethod.Post, new Uri($"https://api.bethesda.net/beam/accounts/login/{loginData!.Username}"));
        msg.Headers.Add("X-Client-API-key", ClientApiKey);
        msg.Headers.Add("x-src-fp", FingerprintKey);
        msg.Headers.Add("X-Platform", AgentPlatform);
        msg.Content = new StringContent(JsonSerializer.Serialize(new BeamLogin
        {
            Password = loginData.Password,
            Language = AgentLanguage
        }, _jsonOptions), Encoding.UTF8, "application/json");

        var result = await _httpClient.SendAsync(msg, token);
        if (!result.IsSuccessStatusCode)
            throw new HttpException(result);
        
        var response = await result.Content.ReadFromJsonAsync<BeamLoginResponse>(_jsonOptions, token);
        loginData.BeamResponse = response;

        await _tokenProvider.SetToken(loginData);
    }

    public async Task CdpAuth(CancellationToken token)
    {
        var state = await _tokenProvider.Get();
        if (string.IsNullOrEmpty(state!.BeamResponse?.AccessToken))
            throw new Exception("Can't get CDPAuth before Bethesda Net login");
        
        var msg = MakeMessage(HttpMethod.Post, new Uri("https://api.bethesda.net/cdp-user/auth"));
        msg.Headers.Add("x-src-fp", FingerprintKey);
        msg.Headers.Add("x-cdp-app", "UGC SDK");
        msg.Headers.Add("x-cdp-app-ver", "0.9.11314/debug");
        msg.Headers.Add("x-cdp-lib-ver", "0.9.11314/debug");
        msg.Headers.Add("x-cdp-platform", "Win/32");
        msg.Content = new StringContent(JsonSerializer.Serialize(new CDPAuthPost()
            {AccessToken = state.BeamResponse.AccessToken}), Encoding.UTF8, "application/json");

        var request = await _httpClient.SendAsync(msg, token);
        if (!request.IsSuccessStatusCode)
            throw new HttpException(request);

        _entitlementData = await request.Content.ReadFromJsonAsync<CDPAuthResponse>(_jsonOptions, token);
        
    }

    private HttpRequestMessage MakeMessage(HttpMethod method, Uri uri)
    {
        var msg = new HttpRequestMessage(method, uri);
        msg.Headers.Add("User-Agent", "bnet");
        msg.Headers.Add("Accept", "application/json");
        msg.Headers.Add("X-BNET-Agent", AgentVersion);
        return msg;
    }

    private void SetFingerprint()
    {
        var keyBytes = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(keyBytes);

        FingerprintKey = string.Concat(Array.ConvertAll(keyBytes, x => x.ToString("X2")));
    }

    public async Task<IEnumerable<(Content Content, Bethesda State)>> ListContent(Game game, CancellationToken token)
    {
        var gameKey = game switch
        {
            Game.SkyrimSpecialEdition => "SKYRIM",
            Game.Fallout4 => "FALLOUT4",
            _ => throw new InvalidOperationException("Only Skyrim and Fallout 4 are supported for Bethesda Net content")
        };

        await EnsureAuthed(token);
        var authData = await _tokenProvider.Get();
        var msg = MakeMessage(HttpMethod.Get,
            new Uri(
                $"https://api.bethesda.net/mods/ugc-workshop/list?page=1;sort=alpha;order=asc;number_results=500;platform=WINDOWS;product={gameKey};cc_mod=true"));
        msg.Headers.Add("X-Access-Token", authData!.BeamResponse!.AccessToken);
        var request = await _httpClient.SendAsync(msg, token);
        if (!request.IsSuccessStatusCode)
            throw new HttpException(request);
        var response = await request.Content.ReadFromJsonAsync<ListSubscribeResponse>(_jsonOptions, token);
        return response!.Platform.Response.Content
            .Select(c => (c, new Bethesda
            {
                Game = game,
                ContentId = c.ContentId,
                IsCCMod = c.CcMod,
                ProductId = c.CdpProductId,
                BranchId = c.CdpBranchId
            }));
    }

    private async Task EnsureAuthed(CancellationToken token)
    {
        if (_entitlementData == null)
            await CdpAuth(token);
    }
    
    public async Task<Depot?> GetDepots(Bethesda state, CancellationToken token)
    {
        return (await MakeCdpRequest<Dictionary<string, Depot>>(state, "depots", token))?.Values.First();
    }

    public async Task<Tree?> GetTree(Bethesda state, CancellationToken token)
    {
        return await MakeCdpRequest<Tree>(state, "tree", token);
    }

    private async Task<T?> MakeCdpRequest<T>(Bethesda state, string type, CancellationToken token)
    {
        await EnsureAuthed(token);
        var msg = MakeMessage(HttpMethod.Get,
            new Uri($"https://api.bethesda.net/cdp-user/projects/{state.ProductId}/branches/{state.BranchId}/{type}/.json"));
        msg.Headers.Add("x-src-fp", FingerprintKey);
        msg.Headers.Add("x-cdp-app", "UGC SDK");
        msg.Headers.Add("x-cdp-app-ver", "0.9.11314/debug");
        msg.Headers.Add("x-cdp-lib-ver", "0.9.11314/debug");
        msg.Headers.Add("x-cdp-platform", "Win/32");
        msg.Headers.Add("Authorization", $"Token {_entitlementData!.Token}");

        using var request = await _httpClient.SendAsync(msg, token);
        if (!request.IsSuccessStatusCode)
            throw new HttpException(request);
        
        var response = await request.Content.ReadFromJsonAsync<T>(_jsonOptions, token);
        return response;
    }
}