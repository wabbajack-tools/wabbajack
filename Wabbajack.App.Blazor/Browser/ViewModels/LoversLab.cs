using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Blazor.Browser.ViewModels;

public class LoversLab : IPSOAuth2Login<LoversLabLoginState>
{
    public LoversLab(ILogger<LoversLab> logger, HttpClient httpClient, EncryptedJsonTokenProvider<LoversLabLoginState> tokenProvider) 
        : base(logger, httpClient, tokenProvider)
    {
    }
}
