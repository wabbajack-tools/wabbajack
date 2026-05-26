using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.OAuth;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.CLI.Verbs;

public class NexusLogin
{
    private const string OAuthUrl = "https://users.nexusmods.com/oauth";
    private const string OAuthRedirectUrl = "https://127.0.0.1:1234";
    private const string OAuthClientId = "wabbajack";

    private readonly ILogger<NexusLogin> _logger;
    private readonly ITokenProvider<NexusOAuthState> _tokenProvider;
    private readonly HttpClient _client;
    private readonly INexusOAuthFlow _flow;

    public NexusLogin(ILogger<NexusLogin> logger, ITokenProvider<NexusOAuthState> tokenProvider,
        HttpClient client, INexusOAuthFlow flow)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _client = client;
        _flow = flow;
    }

    public static VerbDefinition Definition = new("nexus-login",
        "Authenticate with Nexus Mods via browser OAuth",
        Array.Empty<OptionDefinition>());

    public async Task<int> Run(CancellationToken token)
    {
        var codeVerifier = Base64UrlEncode(Guid.NewGuid().ToByteArray());
        var codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)));
        var state = Guid.NewGuid().ToString("N");

        var authUrl = BuildAuthorizeUrl(codeChallenge, state);

        var code = await _flow.GetAuthorizationCode(authUrl, state, token);
        if (code == null)
            return 1;

        var jwt = await ExchangeCode(codeVerifier, code, token);
        if (jwt == null)
            return 1;

        jwt.ReceivedAt = DateTime.UtcNow.ToFileTimeUtc();
        await _tokenProvider.SetToken(new NexusOAuthState { OAuth = jwt, ApiKey = string.Empty });
        _logger.LogInformation("Nexus Mods login successful");
        return 0;
    }

    internal static Uri BuildAuthorizeUrl(string challenge, string state)
    {
        var queryString = string.Join("&", new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["scope"] = "public openid profile",
            ["code_challenge_method"] = "S256",
            ["client_id"] = OAuthClientId,
            ["redirect_uri"] = OAuthRedirectUrl,
            ["code_challenge"] = challenge,
            ["state"] = state,
        }.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return new Uri($"{OAuthUrl}/authorize?{queryString}");
    }

    internal async Task<JwtTokenReply?> ExchangeCode(string verifier, string code, CancellationToken token)
    {
        var response = await _client.PostAsync($"{OAuthUrl}/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = OAuthClientId,
                ["redirect_uri"] = OAuthRedirectUrl,
                ["code"] = code,
                ["code_verifier"] = verifier,
            }), token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token exchange failed: {StatusCode} {Reason}",
                response.StatusCode, response.ReasonPhrase);
            return null;
        }

        return JsonSerializer.Deserialize<JwtTokenReply>(
            await response.Content.ReadAsStringAsync(token));
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
