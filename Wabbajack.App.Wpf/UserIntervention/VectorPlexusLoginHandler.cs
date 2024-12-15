using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

public class VectorPlexusLoginHandler : OAuth2LoginHandler<VectorPlexusLoginState>
{
    public VectorPlexusLoginHandler(ILogger<VectorPlexusLoginHandler> logger, HttpClient httpClient, EncryptedJsonTokenProvider<VectorPlexusLoginState> tokenProvider, IServiceProvider serviceProvider) 
        : base(logger, httpClient, tokenProvider, serviceProvider)
    {
    }
}