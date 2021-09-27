using System;
using System.Net.Http;
using CefNet;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.App.ViewModels
{
    public class LoversLabOAuthLoginViewModel : OAuthLoginViewModel<LoversLabLoginState>
    {
        public LoversLabOAuthLoginViewModel(ILogger<LoversLabOAuthLoginViewModel> logger, HttpClient client, 
            LoversLabTokenProvider tokenProvider)
            : base(logger, client, tokenProvider)
        {
        }
    }
}