using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Networking.Steam.UserInterventions;

public class GetAuthCode : AUserIntervention<string>
{
    public enum AuthType
    {
        TwoFactorAuth,
        EmailCode
    }
    public GetAuthCode(AuthType type) => Type = type;
    public AuthType Type { get; }
}