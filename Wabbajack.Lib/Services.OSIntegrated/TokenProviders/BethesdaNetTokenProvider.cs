using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.Services.OSIntegrated.TokenProviders;

public class BethesdaNetTokenProvider : EncryptedJsonTokenProvider<BethesdaNetLoginState>
{
    public BethesdaNetTokenProvider(ILogger<BethesdaNetLoginState> logger, DTOSerializer dtos) : base(logger, dtos,
        "bethesda-net")
    {
    }
}