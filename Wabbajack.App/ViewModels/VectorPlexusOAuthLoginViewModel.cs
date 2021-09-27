using System;
using System.Net.Http;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.App.ViewModels
{
    public class VectorPlexusOAuthLoginViewModel : OAuthLoginViewModel<VectorPlexusLoginState>
    {
        public VectorPlexusOAuthLoginViewModel(ILogger<LoversLabOAuthLoginViewModel> logger, HttpClient client, 
            VectorPlexusTokenProvider tokenProvider)
            : base(logger, client, tokenProvider)
        {
        }
    }
}