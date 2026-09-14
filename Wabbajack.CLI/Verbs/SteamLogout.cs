using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Networking.Steam;

namespace Wabbajack.CLI.Verbs;

public class SteamLogout
{
    private readonly ILogger<SteamLogout> _logger;
    private readonly ISteamSession _session;

    public SteamLogout(ILogger<SteamLogout> logger, ISteamSession session)
    {
        _logger = logger;
        _session = session;
    }

    public static VerbDefinition Definition = new VerbDefinition("steam-logout",
        "Logs out of Steam and deletes the saved login", []);

    public async Task<int> Run()
    {
        switch (await _session.LogoutAsync())
        {
            case SteamLogoutResult.Deleted:
                _logger.LogInformation("Logged out of Steam and deleted the saved login");
                return 0;

            case SteamLogoutResult.HeldInEnvironment:
                _logger.LogWarning(
                    "Logged out of Steam, but the login comes from the STEAM_LOGIN environment variable and is still in place. Unset it to finish logging out");
                return 0;

            default:
                _logger.LogInformation("There was no saved Steam login to delete");
                return 0;
        }
    }
}
