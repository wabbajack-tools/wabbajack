using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.Services.OSIntegrated.TokenProviders;

public class SteamTokenProvider : EncryptedJsonTokenProvider<SteamLoginState>
{
    public SteamTokenProvider(ILogger<SteamTokenProvider> logger, DTOSerializer dtos) : base(logger, dtos,
        "steam-login")
    {
    }
}
