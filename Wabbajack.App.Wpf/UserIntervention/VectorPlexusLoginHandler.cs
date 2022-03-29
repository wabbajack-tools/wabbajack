using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.UserIntervention;

public class VectorPlexusLoginHandler : OAuth2LoginHandler<Messages.VectorPlexusLogin, DTOs.Logins.VectorPlexusLoginState>
{
    public VectorPlexusLoginHandler(ILogger<VectorPlexusLoginHandler> logger, HttpClient client, ITokenProvider<DTOs.Logins.VectorPlexusLoginState> tokenProvider, 
        WebBrowserVM browser, CefService service) 
        : base(logger, client, tokenProvider, browser, service)
    {
    }
}