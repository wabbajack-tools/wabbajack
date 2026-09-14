namespace Wabbajack.Networking.Steam;

/// <summary>
///     Asks the user for whatever Steam Guard wants mid-login. Implemented by whichever host is driving the
///     login -- the console for the CLI, an intervention for the app.
///     Returning null from either code method means the user gave up. SteamKit's own retry loops are infinite
///     and throw on a null code, so a cancelled prompt is the only way out of a login that is waiting on a
///     human; see <see cref="SteamAuthenticator" />.
/// </summary>
public interface ISteamGuardPrompt
{
    /// <summary>
    ///     A code from the Steam mobile authenticator. Null to cancel the login.
    /// </summary>
    Task<string?> GetDeviceCodeAsync(bool previousCodeWasIncorrect, CancellationToken token);

    /// <summary>
    ///     A code Steam emailed to <paramref name="email" />. Null to cancel the login.
    /// </summary>
    Task<string?> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect, CancellationToken token);

    /// <summary>
    ///     True to wait for the user to approve the login in the Steam mobile app, false to fall back to typing
    ///     a code instead.
    /// </summary>
    Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token);
}
