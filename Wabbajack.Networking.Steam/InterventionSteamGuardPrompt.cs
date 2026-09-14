using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Steam.UserInterventions;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     The default <see cref="ISteamGuardPrompt" />: raises a <see cref="GetAuthCode" /> intervention and waits
///     for whichever host is listening to answer it. Hosts without a UI (the CLI) replace this.
/// </summary>
public class InterventionSteamGuardPrompt : ISteamGuardPrompt
{
    private readonly IUserInterventionHandler _handler;

    public InterventionSteamGuardPrompt(IUserInterventionHandler handler)
    {
        _handler = handler;
    }

    public Task<string?> GetDeviceCodeAsync(bool previousCodeWasIncorrect, CancellationToken token)
    {
        return Ask(new GetAuthCode(GetAuthCode.AuthType.TwoFactorAuth, null, previousCodeWasIncorrect), token);
    }

    public Task<string?> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect, CancellationToken token)
    {
        return Ask(new GetAuthCode(GetAuthCode.AuthType.EmailCode, email, previousCodeWasIncorrect), token);
    }

    public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token)
    {
        return Task.FromResult(true);
    }

    private async Task<string?> Ask(GetAuthCode intervention, CancellationToken token)
    {
        _handler.Raise(intervention);

        await using var registration = token.Register(() =>
        {
            // Racing a Finish() that has already landed; AUserIntervention throws rather than ignoring it.
            try
            {
                if (!intervention.Handled) intervention.Cancel();
            }
            catch (InvalidOperationException)
            {
            }
        });

        try
        {
            return await intervention.Task.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
