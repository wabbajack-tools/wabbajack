using SteamKit2.Authentication;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Bridges SteamKit's mid-login Steam Guard callbacks onto an <see cref="ISteamGuardPrompt" />.
///     Two things about SteamKit shape this class. Its retry loops around these methods are infinite and throw
///     on a null return, so cancellation has to be raised from in here -- there is no outer knob to turn. And
///     the calls happen on the polling loop, so every method has to be genuinely asynchronous: blocking one to
///     wait for a human stalls the whole session.
/// </summary>
public class SteamAuthenticator : IAuthenticator
{
    private readonly ISteamGuardPrompt _prompt;
    private readonly CancellationToken _token;

    public SteamAuthenticator(ISteamGuardPrompt prompt, CancellationToken token)
    {
        _prompt = prompt;
        _token = token;
    }

    public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        _token.ThrowIfCancellationRequested();
        var code = await _prompt.GetDeviceCodeAsync(previousCodeWasIncorrect, _token).ConfigureAwait(false);
        return Require(code);
    }

    public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
    {
        _token.ThrowIfCancellationRequested();
        var code = await _prompt.GetEmailCodeAsync(email, previousCodeWasIncorrect, _token).ConfigureAwait(false);
        return Require(code);
    }

    public async Task<bool> AcceptDeviceConfirmationAsync()
    {
        _token.ThrowIfCancellationRequested();
        return await _prompt.AcceptDeviceConfirmationAsync(_token).ConfigureAwait(false);
    }

    private static string Require(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new OperationCanceledException("The Steam Guard prompt was cancelled");
        return code;
    }
}
