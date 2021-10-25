using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Server.TokenProviders;

public interface IDiscordToken : ITokenProvider<string>
{
}