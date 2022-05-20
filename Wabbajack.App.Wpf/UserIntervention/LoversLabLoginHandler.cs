using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

public class LoversLabLoginHandler : OAuth2LoginHandler<DTOs.Logins.LoversLabLoginState>
{
    public LoversLabLoginHandler(ILogger<LoversLabLoginHandler> logger, HttpClient httpClient, EncryptedJsonTokenProvider<LoversLabLoginState> tokenProvider) 
        : base(logger, httpClient, tokenProvider)
    {
    }
}