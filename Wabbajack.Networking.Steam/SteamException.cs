using SteamKit2;

namespace Wabbajack.Networking.Steam;

public class SteamException : Exception
{
    public SteamException(string message, EResult result, EResult extendedResult) : base(
        $"{message} {result} / {extendedResult}")
    {
        Result = result;
        ExtendedResult = extendedResult;
    }

    public EResult Result { get; }
    public EResult ExtendedResult { get; }
}

/// <summary>
///     There is no usable stored credential, so the user has to authenticate again. Raised in place of a
///     password error whenever <see cref="SteamResults.IsDeadCredential" /> is true, because "your password is
///     wrong" is never what an expired token means.
/// </summary>
public class SteamLoginRequiredException : Exception
{
    public SteamLoginRequiredException(string message) : base(message)
    {
    }
}
