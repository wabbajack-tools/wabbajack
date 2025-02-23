using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.Services.OSIntegrated.TokenProviders;

public class MegaTokenProvider : EncryptedJsonTokenProvider<MegaToken>
{
    public MegaTokenProvider(ILogger<MegaTokenProvider> logger, DTOSerializer dtos) : base(logger, dtos, "mega-login")
    {
    }
}