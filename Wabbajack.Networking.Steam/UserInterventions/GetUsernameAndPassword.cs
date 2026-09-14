using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Networking.Steam.UserInterventions;

/// <summary>
///     A Steam account name and password, held in memory for the length of one login and never stored.
///     Deliberately a class with a hand-written <see cref="ToString" /> rather than a positional record: a
///     record's generated one prints every property, so a single structured log line would put the password
///     in the log file.
/// </summary>
public class SteamCredentials
{
    public SteamCredentials(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public string Username { get; }

    public string Password { get; }

    public override string ToString()
    {
        return $"SteamCredentials {{ Username = {Username} }}";
    }
}

/// <summary>
///     Asks the user for a Steam account name and password. The password is handed straight to the
///     authentication flow and never written anywhere.
/// </summary>
public class GetUsernameAndPassword : AUserIntervention<SteamCredentials>
{
}
