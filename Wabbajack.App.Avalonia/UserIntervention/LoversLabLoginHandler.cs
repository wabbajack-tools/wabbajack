using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

public class LoversLabLoginHandler : OAuth2LoginHandler<LoversLabLoginState>
{
    public LoversLabLoginHandler(ILogger<LoversLabLoginHandler> logger, HttpClient httpClient, EncryptedJsonTokenProvider<LoversLabLoginState> tokenProvider, IServiceProvider serviceProvider) 
        : base(logger, httpClient, tokenProvider, serviceProvider)
    {
    }
}