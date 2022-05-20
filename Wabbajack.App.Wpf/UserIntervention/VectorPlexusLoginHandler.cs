using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

public class VectorPlexusLoginHandler : OAuth2LoginHandler<DTOs.Logins.VectorPlexusLoginState>
{
    public VectorPlexusLoginHandler(ILogger<VectorPlexusLoginHandler> logger, HttpClient httpClient, EncryptedJsonTokenProvider<VectorPlexusLoginState> tokenProvider) 
        : base(logger, httpClient, tokenProvider)
    {
    }
}