using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.NexusApi;

/// <summary>
///     Logging in to Nexus Mods, in the user's own browser. The app opens the authorize URL with the system
///     browser, listens on one loopback port for the redirect, and trades the code it brings back for a
///     token.
///     <para>
///         It used to happen in an embedded WebView2: a window of our own, with its own cookie jar, which
///         meant logging in again even when the user's browser was already signed in to Nexus Mods, and
///         which showed a password field inside an app - the thing every OAuth provider asks clients not to
///         do. The redirect was caught by watching the WebView for a navigation to a host that never had to
///         exist, so nothing was ever listening on it. It is a real socket now, which is why the redirect
///         URI is read off <see cref="LoopbackOAuthCallback.RedirectUri" /> rather than written down here:
///         the port is whatever the OS handed out, and the authorize request and the token exchange have to
///         say the same one.
///     </para>
///     <para>
///         Nothing here throws for an ordinary refusal. Both callers are UI paths that fire and forget - a
///         command handler and a settings tile - so a failed login is a logged false, and what is stored is
///         left exactly as it was.
///     </para>
/// </summary>
public class NexusOAuthLogin
{
    public const string ClientId = "wabbajack";
    private const string OAuthUrl = "https://users.nexusmods.com/oauth";

    private readonly HttpClient _client;
    private readonly IOAuthBrowser _browser;
    private readonly ILogger<NexusOAuthLogin> _logger;
    private readonly ITokenProvider<NexusOAuthState> _tokenProvider;

    public NexusOAuthLogin(ILogger<NexusOAuthLogin> logger, HttpClient client,
        ITokenProvider<NexusOAuthState> tokenProvider, IOAuthBrowser browser)
    {
        _logger = logger;
        _client = client;
        _tokenProvider = tokenProvider;
        _browser = browser;
    }

    /// <summary>
    ///     Runs the whole flow and stores the login it ends with. False for every way it can end without
    ///     one: the user closed the browser, Nexus Mods refused, or the token endpoint gave back something
    ///     that is not a token. Cancellation - which is how a caller times the login out - throws, because
    ///     that is the caller's own token coming back to it.
    /// </summary>
    public async Task<bool> Login(CancellationToken token)
    {
        using var callback = new LoopbackOAuthCallback(_logger);

        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.1
        var codeVerifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.2
        var codeChallenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)));

        var state = Guid.NewGuid().ToString();
        var authorize = GenerateAuthorizeUrl(callback.RedirectUri, codeChallenge, state);

        _logger.LogInformation("Opening the Nexus Mods login in your browser; waiting for {RedirectUri}",
            callback.RedirectUri);

        if (!_browser.Open(authorize))
        {
            _logger.LogError("Could not open a browser for the Nexus Mods login");
            return false;
        }

        var redirect = await callback.WaitForRedirect(token);

        if (redirect.TryGetValue("error", out var error))
        {
            redirect.TryGetValue("error_description", out var description);
            _logger.LogError("Nexus Mods refused the login: {Error} {Description}", error, description);
            return false;
        }

        // The state is the only thing tying this redirect to the request this app made. Anything on the
        // machine can reach a loopback port, so a code arriving without it is not answered.
        if (!redirect.TryGetValue("state", out var received) || received != state)
        {
            _logger.LogError("Ignoring an OAuth redirect that does not belong to this login");
            return false;
        }

        if (!redirect.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            _logger.LogError("The Nexus Mods redirect carried no authorization code");
            return false;
        }

        var next = StateToStore(await Stored(), await AuthorizeToken(callback.RedirectUri, codeVerifier, code, token));
        if (next == null)
        {
            // Nexus did not hand back a login, so whatever is stored is left exactly as it is. This flow is
            // reachable while already logged in - that is what preflight's "log in again" row asks for - so
            // writing a refusal over a working login is a real way to lose one.
            _logger.LogError("Nexus Mods did not return a login; anything already stored is unchanged");
            return false;
        }

        await _tokenProvider.SetToken(next);
        _logger.LogInformation("Logged in to Nexus Mods");
        return true;
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

    private async Task<JwtTokenReply?> AuthorizeToken(string redirectUri, string verifier, string code,
        CancellationToken cancel)
    {
        var request = new Dictionary<string, string>
        {
            {"grant_type", "authorization_code"},
            {"client_id", ClientId},
            {"redirect_uri", redirectUri},
            {"code", code},
            {"code_verifier", verifier}
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
            // the callers of this are fire-and-forget UI paths.
            _logger.LogCritical(ex, "Nexus Mods returned something that is not a token");
            return null;
        }
    }

    internal static Uri GenerateAuthorizeUrl(string redirectUri, string challenge, string state)
    {
        var request = new Dictionary<string, string>
        {
            {"response_type", "code"},
            {"scope", "public openid profile"},
            {"code_challenge_method", "S256"},
            {"client_id", ClientId},
            {"redirect_uri", redirectUri},
            {"code_challenge", challenge},
            {"state", state}
        };

        var query = new StringBuilder();
        foreach (var (name, value) in request)
        {
            query.Append(query.Length == 0 ? '?' : '&');
            query.Append(Uri.EscapeDataString(name));
            query.Append('=');
            query.Append(Uri.EscapeDataString(value));
        }

        return new Uri($"{OAuthUrl}/authorize{query}");
    }
}
