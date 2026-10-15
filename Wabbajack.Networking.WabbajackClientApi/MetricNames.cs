namespace Wabbajack.Networking.WabbajackClientApi;

public static class MetricNames
{
    public const string Downloading = "downloading";
    public const string BeginInstall = "begin_install";
    public const string FinishInstall = "finish_install";

    /// <summary>A Steam login through Wabbajack's own login pane. The subject is how: "qr" or "password".</summary>
    public const string SteamLogin = "steam_login";

    /// <summary>A Bethesda.net sign-in with a Steam app ticket. The subject is the game.</summary>
    public const string BethesdaLogin = "bethesda_login";
}