using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Networking.Browser.Verbs;

public class VectorPlexusLogin : AOAuthLoginVerb<VectorPlexusLoginState>
{
    public VectorPlexusLogin(ILogger<LoverLabLogin> logger, EncryptedJsonTokenProvider<VectorPlexusLoginState> tokenProvider, HttpClient client) : 
        base(logger, "lovers-lab", tokenProvider, client)
    {
    }
}