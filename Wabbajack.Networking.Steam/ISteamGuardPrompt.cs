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
    ///     Called when the account can approve the login from the Steam mobile app. Return true to wait for
    ///     that approval.
    ///     SteamKit will fall back to asking for a typed code if this returns false, but no implementation
    ///     does: both always wait for the app, because offering the choice needs somewhere to offer it, and
    ///     nothing in the tree has that yet. Do not treat the fallback as available until one does.
    /// </summary>
    Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token);
}
