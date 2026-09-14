using System;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.Logins;

/// <summary>
///     The persisted half of a Steam login.
///     Only the account name, the refresh token and Steam's guard data are kept. The password, the TOTP secret
///     and the short lived access token are never written to disk, by design -- see <see cref="RefreshToken" />.
/// </summary>
public class SteamLoginState
{
    [JsonPropertyName("account_name")] public string AccountName { get; set; } = string.Empty;

    /// <summary>
    ///     The long lived refresh token handed back by the authentication flow. It is redeemed as
    ///     <c>SteamUser.LogOnDetails.AccessToken</c> on later logins, which is why no password needs storing.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     Steam Guard machine data, fed back as <c>AuthSessionDetails.GuardData</c> so a returning user is not
    ///     emailed a fresh code on every login. Null when Steam has none for this account.
    /// </summary>
    [JsonPropertyName("guard_data")]
    public string? GuardData { get; set; }

    /// <summary>
    ///     The <c>exp</c> claim of <see cref="RefreshToken" />, decoded when the token was stored. Steam does not
    ///     document the lifetime of a refresh token anywhere, so the token's own claim is the only honest signal
    ///     we have. Null when the token could not be decoded, which is treated as "unknown, try it and see".
    /// </summary>
    [JsonPropertyName("refresh_token_expires_at")]
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
}
