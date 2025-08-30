using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.BethesdaNet;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.CLI.Verbs;

public class BethesdaLogin
{
    private readonly ILogger<BethesdaLogin> _logger;
    private readonly Client _apiClient;
    private readonly ITokenProvider<BethesdaNetLoginState> _tokenProvider;

    public BethesdaLogin(ILogger<BethesdaLogin> logger, ITokenProvider<BethesdaNetLoginState> tokenProvider, Client apiClient)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _apiClient = apiClient;
    }

    public static VerbDefinition Definition = new VerbDefinition("bethesda-login",
        "Sets a Bethesda.NET login token based on the provided username and password",
        [
            new OptionDefinition(typeof(string), "u", "username", "Username for the user account"),
            new OptionDefinition(typeof(string), "p", "password", "Password for the user account"),
        ]);


    public async Task<int> Run(string username, string password)
    {
        _logger.LogInformation("Logging into Bethesda.NET");
        await _tokenProvider.SetToken(new BethesdaNetLoginState
        {
            Username = username,
            Password = password
        });
        await _apiClient.Login(CancellationToken.None);
        _logger.LogInformation("Logged in");
        return 0;
    }
}