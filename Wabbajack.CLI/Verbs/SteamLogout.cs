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
        var deleted = await _session.LogoutAsync();
        _logger.LogInformation(deleted
            ? "Logged out of Steam and deleted the saved login"
            : "There was no saved Steam login to delete");
        return 0;
    }
}
