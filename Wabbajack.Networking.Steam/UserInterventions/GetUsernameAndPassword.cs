using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Networking.Steam.UserInterventions;

public record SteamCredentials(string Username, string Password);

/// <summary>
///     Asks the user for a Steam account name and password. Only ever held in memory: the password is handed
///     straight to the authentication flow and never stored.
/// </summary>
public class GetUsernameAndPassword : AUserIntervention<SteamCredentials>
{
}
