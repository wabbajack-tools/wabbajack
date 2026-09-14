using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Networking.Steam.UserInterventions;

public class GetAuthCode : AUserIntervention<string>
{
    public enum AuthType
    {
        /// <summary>
        ///     A code from the Steam mobile authenticator app.
        /// </summary>
        TwoFactorAuth,

        /// <summary>
        ///     A code Steam emailed to the account's address.
        /// </summary>
        EmailCode
    }

    public GetAuthCode(AuthType type, string? email = null, bool previousCodeWasIncorrect = false)
    {
        Type = type;
        Email = email;
        PreviousCodeWasIncorrect = previousCodeWasIncorrect;
    }

    public AuthType Type { get; }

    /// <summary>
    ///     The address Steam sent the code to, when <see cref="Type" /> is <see cref="AuthType.EmailCode" />.
    /// </summary>
    public string? Email { get; }

    /// <summary>
    ///     Set when this is a retry because the last code was rejected, so the prompt can say so.
    /// </summary>
    public bool PreviousCodeWasIncorrect { get; }
}
