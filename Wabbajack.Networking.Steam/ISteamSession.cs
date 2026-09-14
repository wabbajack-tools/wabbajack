namespace Wabbajack.Networking.Steam;

/// <param name="AccountName">The Steam account that is now logged in.</param>
/// <param name="SteamId">The 64 bit SteamID of that account.</param>
/// <param name="UsedStoredToken">
///     True when the login went through a token saved from a previous session rather than a fresh
///     authentication, so a host can tell the user it did not need to ask them anything.
/// </param>
public record SteamLoginResult(string AccountName, ulong SteamId, bool UsedStoredToken);

/// <summary>
///     Owns the connection to Steam and everything about being logged in. Content lives in <see cref="Client" />.
/// </summary>
public interface ISteamSession : IDisposable
{
    bool IsLoggedIn { get; }

    /// <summary>
    ///     The account currently logged in, or null.
    /// </summary>
    string? AccountName { get; }

    /// <summary>
    ///     True when a refresh token from a previous session is on disk. It may still turn out to be expired or
    ///     revoked; only a login can tell.
    /// </summary>
    bool HaveStoredToken { get; }

    /// <summary>
    ///     Logs in with the stored refresh token. Throws <see cref="SteamLoginRequiredException" /> when there
    ///     is no stored token or the stored one is no longer good.
    /// </summary>
    Task<SteamLoginResult> LoginWithStoredTokenAsync(CancellationToken token);

    /// <summary>
    ///     Logs in by having the user scan a QR code in the Steam mobile app.
    ///     <paramref name="onChallengeUrl" /> is called with the URL to render, once up front and again every
    ///     time Steam rotates it. Steam rotates it as a side effect of polling, so later calls arrive on the
    ///     polling thread rather than the caller's -- marshal before touching anything thread-bound.
    ///     This flow needs the mobile app: its only confirmation type is device confirmation and there is no
    ///     code fallback, so an account without the authenticator has to use
    ///     <see cref="LoginWithCredentialsAsync" />.
    /// </summary>
    Task<SteamLoginResult> LoginWithQrCodeAsync(Action<string> onChallengeUrl, CancellationToken token);

    /// <summary>
    ///     Logs in with an account name and password. Any Steam Guard step is asked for mid-flow through the
    ///     session's <see cref="ISteamGuardPrompt" />. The password is used once and never stored.
    /// </summary>
    Task<SteamLoginResult> LoginWithCredentialsAsync(string username, string password, CancellationToken token);

    /// <summary>
    ///     Logs off and deletes the stored token. Returns true when there was a stored token to delete.
    /// </summary>
    ValueTask<bool> LogoutAsync();
}
