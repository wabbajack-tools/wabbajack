using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI.Verbs;

public class SetNexusApiKey
{
    private readonly EncryptedJsonTokenProvider<NexusOAuthState> _tokenProvider;
    private readonly ILogger<SetNexusApiKey> _logger;

    public SetNexusApiKey(EncryptedJsonTokenProvider<NexusOAuthState> tokenProvider, ILogger<SetNexusApiKey> logger)
    {
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public static VerbDefinition Definition = new("set-nexus-api-key",
        "Sets the Nexus API key to the specified value",
        [
            new OptionDefinition(typeof(string), "k", "key", "The Nexus API key")
        ]);

    public async Task<int> Run(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogInformation("Not setting Nexus API key, that looks like an empty string to me.");
            return -1;
        }
        else
        {
            await _tokenProvider.SetToken(new() { ApiKey = key });
            _logger.LogInformation("Set Nexus API Key to {key}", key);
            return 0;
        }
    }
}