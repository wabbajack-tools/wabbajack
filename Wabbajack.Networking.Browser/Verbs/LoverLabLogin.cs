using System.Net.Http;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Networking.Browser.Verbs;

public class LoverLabLogin : AOAuthLoginVerb<LoversLabLoginState>
{
    public LoverLabLogin(ILogger<LoverLabLogin> logger, EncryptedJsonTokenProvider<LoversLabLoginState> tokenProvider, HttpClient client) : 
        base(logger, "vector-plexus", tokenProvider, client)
    {
    }
}