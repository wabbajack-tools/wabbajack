using SteamKit2;

namespace Wabbajack.Networking.Steam;

public class SteamException : Exception
{
    public EResult Result { get; }
    public EResult ExtendedResult { get; }
    
    public SteamException(string message, EResult result, EResult extendedResult) : base($"{message} {result} / {extendedResult}")
    {
        Result = result;
        ExtendedResult = extendedResult;
    }
    
}