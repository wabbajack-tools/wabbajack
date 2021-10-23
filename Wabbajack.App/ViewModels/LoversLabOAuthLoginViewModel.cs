using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.App.ViewModels;

public class LoversLabOAuthLoginViewModel : OAuthLoginViewModel<LoversLabLoginState>
{
    public LoversLabOAuthLoginViewModel(ILogger<LoversLabOAuthLoginViewModel> logger, HttpClient client,
        LoversLabTokenProvider tokenProvider)
        : base(logger, client, tokenProvider)
    {
    }
}