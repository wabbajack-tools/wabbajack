using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

public class NexusLoginHandler : BrowserWindowViewModel
{
    private static Uri OAuthUrl = new Uri("https://users.nexusmods.com/oauth");
    private static string OAuthRedirectUrl = "https://127.0.0.1:1234";
    private static string OAuthClientId = "wabbajack";
    
    private readonly EncryptedJsonTokenProvider<NexusOAuthState> _tokenProvider;
    private readonly ILogger<NexusLoginHandler> _logger;
    private readonly HttpClient _client;

    public NexusLoginHandler(ILogger<NexusLoginHandler> logger, HttpClient client, EncryptedJsonTokenProvider<NexusOAuthState> tokenProvider, IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _logger = logger;
        _client = client;
        HeaderText = "Nexus Login";
        _tokenProvider = tokenProvider;
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
        
        Browser.CoreWebView2.NewWindowRequested += (sender, args) =>
        {
            var uri = new Uri(args.Uri);
            _logger.LogInformation("New Window Requested {Uri}", args.Uri);
            if (uri.Host != "127.0.0.1") return;
            
            codeCompletionSource.TrySetResult(QueryHelpers.ParseQuery(uri.Query));
            args.Handled = true;
        };

        var uri = GenerateAuthorizeUrl(codeChallenge, state);
        await NavigateTo(uri);

        var ctx = await codeCompletionSource.Task.WaitAsync(token);
        
        if (ctx["state"].FirstOrDefault() != state)
        {
            throw new Exception("State mismatch");
        }
        
        var code = ctx["code"].FirstOrDefault();

        var result = await AuthorizeToken(codeVerifier, code, token);
        var next = StateToStore(await Stored(), result);
        if (next == null)
        {
            // Nexus did not hand back a login, so whatever is stored is left exactly as it is. This window
            // is reachable while already logged in - that is what preflight's "log in again" row asks for -
            // so writing a refusal over a working login is a real way to lose one. Nothing is thrown either:
            // the caller of RunBrowserOperation is an async void message handler.
            _logger.LogError("Nexus Mods did not return a login; anything already stored is unchanged");
            return;
        }

        await _tokenProvider.SetToken(next);
    }

    /// <summary>
    ///     What to write to the token store once the authorize round-trip is over, or null when the answer is
    ///     "nothing worth writing": <see cref="AuthorizeToken" /> returns null on any non-2xx, and a reply
    ///     with no access token in it is no more a login than that. Both used to be stored anyway, which
    ///     turned a five-hundred from the token endpoint into a lost login.
    ///     <para>
    ///         The API key half of the stored login is carried over. This exchange says nothing about it, and
    ///         <c>NexusApi</c> falls back to it when the OAuth half is unusable, so dropping it here would
    ///         quietly log out a machine set up with one.
    ///     </para>
    /// </summary>
    public static NexusOAuthState? StateToStore(NexusOAuthState? stored, JwtTokenReply? received)
    {
        if (string.IsNullOrWhiteSpace(received?.AccessToken)) return null;

        received.ReceivedAt = DateTime.UtcNow.ToFileTimeUtc();
        return new NexusOAuthState {OAuth = received, ApiKey = stored?.ApiKey ?? string.Empty};
    }

    /// <summary>
    ///     The login as stored, or null when there is none - including when reading it throws, which is what
    ///     an unreadable store does. What cannot be read cannot be preserved, and must not stop the login
    ///     that has just succeeded from being written.
    /// </summary>
    private async Task<NexusOAuthState?> Stored()
    {
        try
        {
            return _tokenProvider.HaveToken() ? await _tokenProvider.Get() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the stored Nexus login; the new one replaces it");
            return null;
        }
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
        try
        {
            return JsonSerializer.Deserialize<JwtTokenReply>(responseString);
        }
        catch (JsonException ex)
        {
            // A body that is not the reply we asked for is a refusal like any other. It must not throw:
            // the whole browser operation is driven from an async void handler.
            _logger.LogCritical(ex, "Nexus Mods returned something that is not a token");
            return null;
        }
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