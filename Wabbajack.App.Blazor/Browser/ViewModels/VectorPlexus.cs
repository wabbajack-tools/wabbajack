using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Blazor.Browser.ViewModels;

public class VectorPlexus : IPSOAuth2Login<VectorPlexusLoginState>
{
    public VectorPlexus(ILogger<VectorPlexus> logger, HttpClient httpClient, EncryptedJsonTokenProvider<VectorPlexusLoginState> tokenProvider) 
        : base(logger, httpClient, tokenProvider)
    {
    }
}
