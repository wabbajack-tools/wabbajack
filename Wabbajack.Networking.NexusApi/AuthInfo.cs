using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.NexusApi;

public interface AuthInfo : ITokenProvider<NexusOAuthState>
{
}