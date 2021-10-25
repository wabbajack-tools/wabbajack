using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.NexusApi;

public interface ApiKey : ITokenProvider<NexusApiState>
{
}