using System.Threading.Tasks;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http.Interfaces;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

public class ClientTests
{
    private readonly ITokenProvider<SteamLoginState> _token;
    private readonly Client _steamClient;
    private readonly IUserInterventionHandler _userInterventionHandler;

    public ClientTests(ITokenProvider<SteamLoginState> token, Client client, IUserInterventionHandler userInterventionHandler)
    {
        _token = token;
        _steamClient = client;
        _userInterventionHandler = userInterventionHandler;
    }
    
    
    /** TODO: Figure out how to test this
    [Fact]
    public async Task CanGetLogin()
    {
        var token = await _token.Get();
        Assert.NotNull(token);
        Assert.NotEmpty(token!.User);
    }

    [Fact]
    public async Task CanLogin()
    {
        await _steamClient.Connect();
        await _steamClient.Login();

    }
    */
}