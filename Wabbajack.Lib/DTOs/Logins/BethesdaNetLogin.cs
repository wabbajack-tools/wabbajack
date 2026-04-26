using Wabbajack.DTOs.Logins.BethesdaNet;

namespace Wabbajack.DTOs.Logins;

public class BethesdaNetLoginState
{
    public string Username { get; set; }
    public string Password { get; set; }
    public BeamLoginResponse? BeamResponse { get; set; }
}