namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Something the host can offer the user for a check that did not pass. The host maps <see cref="Id" />
///     to whatever it does about it (open a login window, a folder picker, re-run the check).
/// </summary>
public record PreflightAction(string Id, string Title)
{
    public static readonly PreflightAction Login = new("login", "Log in");
    public static readonly PreflightAction Retry = new("retry", "Retry");
    public static readonly PreflightAction BrowseGameFolder = new("browse-game-folder", "Locate game");
    public static readonly PreflightAction ContinueAnyway = new("continue-anyway", "Continue anyway");
    public static readonly PreflightAction Rescan = new("rescan", "Rescan");

    /// <summary>
    ///     Offered by automated-downloads when its pass pushed archives back into the manual queue: the host
    ///     re-runs manual-downloads, which is a different check from the one offering the action.
    /// </summary>
    public static readonly PreflightAction DownloadByHand = new("download-by-hand", "Download by hand");

    /// <summary>
    ///     Offered by game-files when the game came from a store Wabbajack can fetch content from and some
    ///     of the files this list needs are missing or from another version. Strictly an offer: it may want
    ///     a login the user has not given and may not want to, so a host has to ask before running it, and
    ///     the check's detail says what the login would buy. Fixing the game install by hand stays an equal
    ///     way out.
    /// </summary>
    public static readonly PreflightAction RepairGameFiles = new("repair-game-files", "Fetch missing game files");
}
