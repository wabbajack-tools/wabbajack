using System;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     Why a ticket could not be minted. Every member is a different thing for the user to go and do, which
///     is the whole reason there is more than one of them: start Steam, sign in, buy the game, point
///     Wabbajack at the install, wait and try again. Collapsing them into "it didn't work" leaves the user
///     with nothing to act on.
/// </summary>
public enum SteamAppTicketError
{
    /// <summary>A ticket was minted.</summary>
    None = 0,

    /// <summary>Wabbajack cannot find this game's install, or the game does not come from Steam at all.</summary>
    GameNotFound,

    /// <summary>The game is installed, but <c>steam_api64.dll</c> is not in its folder.</summary>
    SteamApiMissing,

    /// <summary>No Steam client is running, and the ticket can only come from one that is.</summary>
    SteamNotRunning,

    /// <summary>Steam is running but would not start a session for the game: usually nobody is signed in yet.</summary>
    InitFailed,

    /// <summary>The shipped <c>steam_api64.dll</c> exports no interface version this knows how to call.</summary>
    InterfaceUnavailable,

    /// <summary>Steam says the signed-in account does not own the game.</summary>
    NotEntitled,

    /// <summary>Steam answered the ticket request with a refusal of its own.</summary>
    TicketRefused,

    /// <summary>Steam accepted the request and no ticket ever arrived.</summary>
    TicketTimedOut,

    /// <summary>The helper process could not be found, run, or understood.</summary>
    HelperFailed
}

/// <summary>
///     The exit codes the ticket helper uses to tell its parent which <see cref="SteamAppTicketError" />
///     happened, and the sentences that go with them.
///     <para>
///         Codes start at 10 so they never collide with the 1 a CLI verb returns for an ordinary failure, and
///         they are written down rather than derived from the enum's ordinal so that reordering the enum
///         cannot silently change what a running helper means by 14.
///     </para>
/// </summary>
public static class SteamAppTicketErrors
{
    public static int ToExitCode(this SteamAppTicketError error)
    {
        return error switch
        {
            SteamAppTicketError.None => 0,
            SteamAppTicketError.GameNotFound => 10,
            SteamAppTicketError.SteamApiMissing => 11,
            SteamAppTicketError.SteamNotRunning => 12,
            SteamAppTicketError.InitFailed => 13,
            SteamAppTicketError.InterfaceUnavailable => 14,
            SteamAppTicketError.NotEntitled => 15,
            SteamAppTicketError.TicketRefused => 16,
            SteamAppTicketError.TicketTimedOut => 17,
            SteamAppTicketError.HelperFailed => 18,
            _ => 18
        };
    }

    /// <summary>
    ///     What a helper exit code meant. Anything unrecognised - including the 1 that comes out of an
    ///     unhandled exception, and whatever a process killed by the OS leaves behind - is
    ///     <see cref="SteamAppTicketError.HelperFailed" />, because an exit code this does not know is a
    ///     statement about the helper and not about Steam.
    /// </summary>
    public static SteamAppTicketError FromExitCode(int exitCode)
    {
        return exitCode switch
        {
            0 => SteamAppTicketError.None,
            10 => SteamAppTicketError.GameNotFound,
            11 => SteamAppTicketError.SteamApiMissing,
            12 => SteamAppTicketError.SteamNotRunning,
            13 => SteamAppTicketError.InitFailed,
            14 => SteamAppTicketError.InterfaceUnavailable,
            15 => SteamAppTicketError.NotEntitled,
            16 => SteamAppTicketError.TicketRefused,
            17 => SteamAppTicketError.TicketTimedOut,
            _ => SteamAppTicketError.HelperFailed
        };
    }

    /// <summary>
    ///     The sentence to show when the helper said nothing more specific. Implementations that know more -
    ///     which folder, which interface, which Steam result - say that instead.
    /// </summary>
    public static string DefaultMessage(this SteamAppTicketError error, string gameName)
    {
        return error switch
        {
            SteamAppTicketError.None => $"Steam handed over an app ticket for {gameName}.",
            SteamAppTicketError.GameNotFound =>
                $"Wabbajack cannot find a Steam install of {gameName}. This content comes from the game's own " +
                "store account, so the game has to be installed through Steam.",
            SteamAppTicketError.SteamApiMissing =>
                $"{gameName} is installed, but steam_api64.dll is not in its folder. Verifying the game's files " +
                "in Steam puts it back.",
            SteamAppTicketError.SteamNotRunning =>
                "Steam is not running. The ticket is minted by the Steam client itself, so start Steam, sign in, " +
                "and try again.",
            SteamAppTicketError.InitFailed =>
                $"Steam is running but would not start a session for {gameName}. Sign in to Steam with the " +
                "account that owns the game, give it a moment to finish starting up, and try again.",
            SteamAppTicketError.InterfaceUnavailable =>
                $"The copy of steam_api64.dll that ships with {gameName} exports no Steamworks interface " +
                "version Wabbajack knows how to call. Verifying the game's files in Steam usually replaces it.",
            SteamAppTicketError.NotEntitled =>
                $"The account signed in to Steam does not own {gameName}, so Steam will not vouch for it.",
            SteamAppTicketError.TicketRefused =>
                "Steam refused to issue an app ticket. That is usually Steam being offline or rate limiting; " +
                "wait a moment and try again.",
            SteamAppTicketError.TicketTimedOut =>
                "Steam accepted the request for an app ticket and never produced one. Check that Steam is " +
                "online rather than in offline mode, and try again.",
            _ => "The Steam app ticket helper did not run properly, so there is no ticket to work with."
        };
    }
}

/// <summary>A ticket could not be minted, and <see cref="Error" /> says which sort of "could not".</summary>
public class SteamAppTicketException : Exception
{
    public SteamAppTicketException(SteamAppTicketError error, string message, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
    }

    public SteamAppTicketError Error { get; }
}
