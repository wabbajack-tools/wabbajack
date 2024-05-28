using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Fizzler.Systems.HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Services.OSIntegrated;
using Cookie = Wabbajack.DTOs.Logins.Cookie;

namespace Wabbajack.UserIntervention;

public class NexusLoginHandler : BrowserWindowViewModel
{
    private static Uri OAuthUrl = new Uri("https://users.nexusmods.com/oauth");
    private static string OAuthRedirectUrl = "https://127.0.0.1:1234";
    private static string OAuthClientId = "wabbajack";
    
    private readonly EncryptedJsonTokenProvider<NexusOAuthState> _tokenProvider;
    private readonly ILogger<NexusLoginHandler> _logger;
    private readonly HttpClient _client;

    public NexusLoginHandler(ILogger<NexusLoginHandler> logger, HttpClient client, EncryptedJsonTokenProvider<NexusOAuthState> tokenProvider)
    {
        _logger = logger;
        _client = client;
        HeaderText = "Nexus Login";
        _tokenProvider = tokenProvider;
    }
    
    private string Base64Id()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    protected override async Task Run(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        
        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.1
        var codeVerifier = Guid.NewGuid().ToString("N").ToBase64();

        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.2
        var codeChallengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = StringBase64Extensions.Base64UrlEncode(codeChallengeBytes);


        Instructions = "Please log into the Nexus";

        var state = Guid.NewGuid().ToString();
        
        await NavigateTo(new Uri("https://nexusmods.com"));
        var codeCompletionSource = new TaskCompletionSource<Dictionary<string, StringValues>>();
        
        Browser!.Browser.CoreWebView2.NewWindowRequested += (sender, args) =>
        {
            var uri = new Uri(args.Uri);
            _logger.LogInformation("New Window Requested {Uri}", args.Uri);
            if (uri.Host != "127.0.0.1") return;
            
            codeCompletionSource.TrySetResult(QueryHelpers.ParseQuery(uri.Query));
            args.Handled = true;
        };

        var uri = GenerateAuthorizeUrl(codeChallenge, state);
        await NavigateTo(uri);

        var ctx = await codeCompletionSource.Task;
        
        if (ctx["state"].FirstOrDefault() != state)
        {
            throw new Exception("State mismatch");
        }
        
        var code = ctx["code"].FirstOrDefault();

        var result = await AuthorizeToken(codeVerifier, code, token);
        
        if (result != null) 
            result.ReceivedAt = DateTime.UtcNow.ToFileTimeUtc();

        await _tokenProvider.SetToken(new NexusOAuthState()
        {
            OAuth = result!
        });
    }
    
    private async Task<JwtTokenReply?> AuthorizeToken(string verifier, string code, CancellationToken cancel)
    {
        var request = new Dictionary<string, string> {
            { "grant_type", "authorization_code" },
            { "client_id", OAuthClientId },
            { "redirect_uri", OAuthRedirectUrl },
            { "code", code },
            { "code_verifier", verifier },
        };

        var content = new FormUrlEncodedContent(request);

        var response = await _client.PostAsync($"{OAuthUrl}/token", content, cancel);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogCritical("Failed to get token {code} - {message}", response.StatusCode,
                response.ReasonPhrase);
            return null;
        }
        var responseString = await response.Content.ReadAsStringAsync(cancel);
        return JsonSerializer.Deserialize<JwtTokenReply>(responseString);
    }
        
    internal static Uri GenerateAuthorizeUrl(string challenge, string state)
    {
        var request = new Dictionary<string, string>
        {
            { "response_type", "code" },
            { "scope", "public openid profile" },
            { "code_challenge_method", "S256" },
            { "client_id", OAuthClientId },
            { "redirect_uri", OAuthRedirectUrl },
            { "code_challenge", challenge },
            { "state", state },
        };
        
        return new Uri(QueryHelpers.AddQueryString($"{OAuthUrl}/authorize", request));
    }
}