using SteamKit2;

namespace Wabbajack.Networking.Steam;

public class SteamException : Exception
{
    public SteamException(string message, EResult result, EResult extendedResult, Exception? inner = null) : base(
        $"{message} {result} / {extendedResult}", inner)
    {
        Result = result;
        ExtendedResult = extendedResult;
    }

    public EResult Result { get; }
    public EResult ExtendedResult { get; }
}

/// <summary>
///     There is no usable stored credential, so the user has to authenticate again. Raised in place of a
///     password error whenever <see cref="SteamResults.IsDeadCredential" /> is true on a token login, because
///     "your password is wrong" is never what an expired token means.
/// </summary>
public class SteamLoginRequiredException : Exception
{
    public SteamLoginRequiredException(string message) : base(message)
    {
    }
}

/// <summary>
///     Steam turned down the account name and password given to a credentials login.
///     This is the one place a password error is the right thing to say. The same
///     <see cref="EResult.InvalidPassword" /> arriving from a token logon means the token is dead, and gets
///     <see cref="SteamLoginRequiredException" /> instead.
/// </summary>
public class SteamCredentialsRejectedException : Exception
{
    public SteamCredentialsRejectedException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
///     A login is already under way on this session. Logins are serialised, and one can sit on a Steam Guard
///     prompt for as long as the user takes, so a second caller is told rather than left to wait on something
///     that looks exactly like a hang.
/// </summary>
public class SteamLoginInProgressException : Exception
{
    public SteamLoginInProgressException(string message) : base(message)
    {
    }
}
