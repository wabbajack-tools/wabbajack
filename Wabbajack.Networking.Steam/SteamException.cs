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

/// <summary>
///     The logged in account holds no licence covering the depot, and the app is not free to download.
///     Separate from a generic failure because it is the one content error the user can actually act on,
///     and the action is "buy or install the thing", not "try again".
/// </summary>
public class SteamNoEntitlementException : Exception
{
    public SteamNoEntitlementException(string message, uint appId, uint depotId) : base(message)
    {
        AppId = appId;
        DepotId = depotId;
    }

    public uint AppId { get; }
    public uint DepotId { get; }
}

/// <summary>
///     Steam refused to issue a manifest request code, which it signals by returning zero rather than an
///     error. It means one of two things and neither is worth retrying: the account cannot reach the depot,
///     or the manifest has been withdrawn -- which is what an old game version eventually becomes.
/// </summary>
public class SteamManifestUnavailableException : Exception
{
    public SteamManifestUnavailableException(string message, uint appId, uint depotId, ulong manifestId) :
        base(message)
    {
        AppId = appId;
        DepotId = depotId;
        ManifestId = manifestId;
    }

    public uint AppId { get; }
    public uint DepotId { get; }
    public ulong ManifestId { get; }
}

/// <summary>The manifest downloaded fine and simply does not contain the file that was asked for.</summary>
public class SteamFileNotInDepotException : Exception
{
    public SteamFileNotInDepotException(string message, string path) : base(message)
    {
        Path = path;
    }

    public string Path { get; }
}

/// <summary>
///     The assembled file does not hash to what the manifest says it should. Every depot file carries its
///     own hash, so this is checkable without trusting anything downstream, and a file that fails is thrown
///     away rather than handed on.
/// </summary>
public class SteamContentVerificationException : Exception
{
    public SteamContentVerificationException(string message) : base(message)
    {
    }
}

/// <summary>
///     Steam's server directory offered nothing that will serve this app. Distinct from every server having
///     failed, which the pool handles by re-asking.
/// </summary>
public class SteamNoContentServersException : Exception
{
    public SteamNoContentServersException(string message) : base(message)
    {
    }
}
