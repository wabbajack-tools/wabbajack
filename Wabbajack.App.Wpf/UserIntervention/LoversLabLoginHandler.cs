using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.UserIntervention;

public class LoversLabLoginHandler : OAuth2LoginHandler<Messages.LoversLabLogin, DTOs.Logins.LoversLabLoginState>
{
    public LoversLabLoginHandler(ILogger<LoversLabLoginHandler> logger, HttpClient client, ITokenProvider<DTOs.Logins.LoversLabLoginState> tokenProvider, 
        WebBrowserVM browser, CefService service) 
        : base(logger, client, tokenProvider, browser, service)
    {
    }
}