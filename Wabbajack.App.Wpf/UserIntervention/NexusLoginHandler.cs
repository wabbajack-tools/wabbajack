#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Networking.NexusApi.OAuth;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

/// <summary>
///     The app's half of the Nexus Mods login: run the flow, decide what came back is worth storing, store
///     it. The flow itself - the loopback redirect, the PKCE pair, the token exchange - is
///     <see cref="NexusOAuthLogin" />, which knows nothing of WPF and can be tested without it.
/// </summary>
public class NexusLoginHandler
{
    private readonly NexusOAuthLogin _login;
    private readonly ILogger<NexusLoginHandler> _logger;
    private readonly EncryptedJsonTokenProvider<NexusOAuthState> _tokenProvider;

    public NexusLoginHandler(ILogger<NexusLoginHandler> logger, NexusOAuthLogin login,
        EncryptedJsonTokenProvider<NexusOAuthState> tokenProvider)
    {
        _logger = logger;
        _login = login;
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    ///     True only when a new login was stored.
    ///     <para>
    ///         Never throws. This is awaited from a fire-and-forget task behind a settings button and from a
    ///         CLI verb, and neither has anywhere to put an exception; every way this can end badly is a
    ///         logged line and a false.
    ///     </para>
    /// </summary>
    public async Task<bool> LogIn(CancellationToken token)
    {
        var result = await _login.LogIn(UIUtils.OpenWebsite, token);

        switch (result.Outcome)
        {
            case NexusOAuthOutcome.Succeeded:
                break;
            case NexusOAuthOutcome.Cancelled:
                _logger.LogInformation("The Nexus Mods login was cancelled; anything already stored is unchanged");
                return false;
            case NexusOAuthOutcome.TimedOut:
                _logger.LogWarning("The Nexus Mods login was never finished: {Detail}", result.Detail);
                return false;
            case NexusOAuthOutcome.Denied:
                _logger.LogWarning("Nexus Mods refused the login: {Detail}", result.Detail);
                return false;
            case NexusOAuthOutcome.AlreadyRunning:
                _logger.LogInformation("{Detail}", result.Detail);
                return false;
            default:
                _logger.LogError("The Nexus Mods login failed: {Detail}", result.Detail);
                return false;
        }

        var next = StateToStore(await Stored(), result.Token);
        if (next == null)
        {
            // Nexus did not hand back a login, so whatever is stored is left exactly as it is. This is
            // reachable while already logged in - that is what preflight's "log in again" row asks for - so
            // writing a refusal over a working login is a real way to lose one.
            _logger.LogError("Nexus Mods did not return a login; anything already stored is unchanged");
            return false;
        }

        await _tokenProvider.SetToken(next);
        return true;
    }

    /// <summary>
    ///     What to write to the token store once the authorize round-trip is over, or null when the answer is
    ///     "nothing worth writing": <see cref="NexusOAuthLogin.AuthorizeToken" /> returns null on any non-2xx,
    ///     and a reply with no access token in it is no more a login than that. Both used to be stored anyway,
    ///     which turned a five-hundred from the token endpoint into a lost login.
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
}
