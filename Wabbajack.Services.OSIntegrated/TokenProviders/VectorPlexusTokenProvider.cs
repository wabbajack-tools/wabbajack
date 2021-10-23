using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.Services.OSIntegrated.TokenProviders;

public class VectorPlexusTokenProvider : EncryptedJsonTokenProvider<VectorPlexusLoginState>
{
    public VectorPlexusTokenProvider(ILogger<VectorPlexusTokenProvider> logger, DTOSerializer dtos)
        : base(logger, dtos, "vector-plexus")
    {
    }
}