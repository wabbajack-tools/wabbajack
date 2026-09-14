using SteamKit2;

namespace Wabbajack.Networking.Steam;

public static class SteamResults
{
    /// <summary>
    ///     True when a logon failure means the stored credential is gone and the user has to authenticate
    ///     again from scratch.
    ///     Steam reports an expired or revoked refresh token as <see cref="EResult.InvalidPassword" />. That is
    ///     emphatically not a wrong password -- a token login never handles a password at all -- and reporting
    ///     it as one sends people off resetting a password that was never the problem. Every result here means
    ///     the same thing to the user: log in again.
    /// </summary>
    public static bool IsDeadCredential(EResult result)
    {
        return result switch
        {
            EResult.InvalidPassword => true,
            EResult.InvalidSignature => true,
            EResult.AccessDenied => true,
            EResult.Expired => true,
            EResult.Revoked => true,
            _ => false
        };
    }
}
