using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Networking.NexusApi.OAuth;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.LoginManagers;

/// <summary>
///     The app's half of the Nexus Mods login: run the flow, decide what came back is worth storing, store it.
///     The flow itself is <see cref="NexusOAuthLogin" />, which knows nothing of any UI. Same rules as the
///     WPF app's: never throws, and a refusal from the token endpoint writes nothing.
/// </summary>
public class NexusLoginHandler(
    ILogger<NexusLoginHandler> logger,
    NexusOAuthLogin login,
    EncryptedJsonTokenProvider<NexusOAuthState> tokenProvider)
{
    /// <summary>True only when a new login was stored.</summary>
    public async Task<bool> LogIn(CancellationToken token)
    {
        var result = await login.LogIn(UIUtils.OpenWebsite, token);

        switch (result.Outcome)
        {
            case NexusOAuthOutcome.Succeeded:
                break;
            case NexusOAuthOutcome.Cancelled:
                logger.LogInformation("The Nexus Mods login was cancelled; anything already stored is unchanged");
                return false;
            case NexusOAuthOutcome.TimedOut:
                logger.LogWarning("The Nexus Mods login was never finished: {Detail}", result.Detail);
                return false;
            case NexusOAuthOutcome.Denied:
                logger.LogWarning("Nexus Mods refused the login: {Detail}", result.Detail);
                return false;
            case NexusOAuthOutcome.AlreadyRunning:
                logger.LogInformation("{Detail}", result.Detail);
                return false;
            default:
                logger.LogError("The Nexus Mods login failed: {Detail}", result.Detail);
                return false;
        }

        var next = StateToStore(await Stored(), result.Token);
        if (next == null)
        {
            logger.LogError("Nexus Mods did not return a login; anything already stored is unchanged");
            return false;
        }

        await tokenProvider.SetToken(next);
        logger.LogInformation("Logged into Nexus Mods; the login is stored");
        return true;
    }

    /// <summary>
    ///     What to write once the authorize round-trip is over, or null for "nothing worth writing". The API
    ///     key half of the stored login is carried over; this exchange says nothing about it.
    /// </summary>
    public static NexusOAuthState? StateToStore(NexusOAuthState? stored, JwtTokenReply? received)
    {
        if (string.IsNullOrWhiteSpace(received?.AccessToken)) return null;

        received.ReceivedAt = DateTime.UtcNow.ToFileTimeUtc();
        return new NexusOAuthState { OAuth = received, ApiKey = stored?.ApiKey ?? string.Empty };
    }

    private async Task<NexusOAuthState?> Stored()
    {
        try
        {
            return tokenProvider.HaveToken() ? await tokenProvider.Get() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the stored Nexus login; the new one replaces it");
            return null;
        }
    }
}
