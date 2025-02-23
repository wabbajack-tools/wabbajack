using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.ModDB;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.CLI.Verbs;

public class MegaLogin
{
    private readonly ILogger<MegaLogin> _logger;
    private readonly MegaApiClient _apiClient;

    public MegaLogin(ILogger<MegaLogin> logger, ITokenProvider<MegaToken> tokenProvider, MegaApiClient apiClient)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _apiClient = apiClient;
    }

    public static VerbDefinition Definition = new VerbDefinition("mega-login",
        "Hashes a file with Wabbajack's hashing routines", new[]
        {
            new OptionDefinition(typeof(string), "e", "email", "Email for the user account"),
            new OptionDefinition(typeof(string), "p", "password", "Password for the user account"),
        });

    private readonly ITokenProvider<MegaToken> _tokenProvider;

    public async Task<int> Run(string email, string password)
    {
        _logger.LogInformation("Logging into Mega");
        await _tokenProvider.SetToken(new MegaToken
        {
            Login = _apiClient.GenerateAuthInfos(email, password)
        });
        return 0;
    }
}