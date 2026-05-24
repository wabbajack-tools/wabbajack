using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.CLI.Verbs;

public class NexusLogin
{
    private readonly ILogger<NexusLogin> _logger;
    private readonly ITokenProvider<NexusOAuthState> _tokenProvider;

    public NexusLogin(ILogger<NexusLogin> logger, ITokenProvider<NexusOAuthState> tokenProvider)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
    }

    public static VerbDefinition Definition = new("nexus-login",
        "Store a Nexus Mods personal API key for use during compilation and installation",
        new[]
        {
            new OptionDefinition(typeof(string), "k", "apiKey",
                "Personal API key from nexusmods.com/users/myaccount?tab=api+access"),
        });

    public async Task<int> Run(string apiKey)
    {
        _logger.LogInformation("Storing Nexus API key");
        await _tokenProvider.SetToken(new NexusOAuthState { ApiKey = apiKey, OAuth = null });
        _logger.LogInformation("Nexus API key stored successfully");
        return 0;
    }
}
