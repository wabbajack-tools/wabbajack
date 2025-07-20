using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

public class NexusAuthHelper
{
    private readonly ILogger<NexusAuthHelper> _logger;
    private readonly HttpClient _client;
    private readonly EncryptedJsonTokenProvider<NexusOAuthState> _tokenProvider;

    public NexusAuthHelper(
        ILogger<NexusAuthHelper> logger,
        HttpClient client,
        EncryptedJsonTokenProvider<NexusOAuthState> tokenProvider)
    {
        _logger = logger;
        _client = client;
        _tokenProvider = tokenProvider;
    }

    public async Task<JwtTokenReply?> AuthorizeToken(string verifier, string code, CancellationToken cancel)
    {
        var request = new Dictionary<string, string> {
            { "grant_type", "authorization_code" },
            { "client_id", Consts.NexusOAuthClientId },
            { "redirect_uri", Consts.NexusOAuthRedirectUrl },
            { "code", code },
            { "code_verifier", verifier },
        };

        var content = new FormUrlEncodedContent(request);

        var response = await _client.PostAsync($"{Consts.NexusOAuthUrl}/token", content, cancel);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogCritical("Failed to get token {code} - {message}", response.StatusCode,
                response.ReasonPhrase);
            return null;
        }
        var responseString = await response.Content.ReadAsStringAsync(cancel);
        return JsonSerializer.Deserialize<JwtTokenReply>(responseString);
    }

    public Uri GenerateAuthorizeUrl(string challenge, string state)
    {
        var request = new Dictionary<string, string>
        {
            { "response_type", "code" },
            { "scope", "public openid profile" },
            { "code_challenge_method", "S256" },
            { "client_id", Consts.NexusOAuthClientId },
            { "redirect_uri", Consts.NexusOAuthRedirectUrl },
            { "code_challenge", challenge },
            { "state", state },
        };
        
        return new Uri(QueryHelpers.AddQueryString($"{Consts.NexusOAuthUrl}/authorize", request));
    }
}
