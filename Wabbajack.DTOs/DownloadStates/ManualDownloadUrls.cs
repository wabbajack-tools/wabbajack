using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Wabbajack.DTOs.DownloadStates;

/// <summary>
///     Where a user has to go to fetch an archive by hand, and what to do once they get there.
/// </summary>
public record ManualDownloadTarget(Uri Url, string SiteName, string Instructions);

/// <summary>
///     Maps a download state to the page a user can open in their own browser to download the file.
///     States that have no browser-reachable page (game files, Creation Club content, legacy sources)
///     return false.
/// </summary>
public static class ManualDownloadUrls
{
    private const string LoversLabSite = "https://www.loverslab.com";
    private const string VectorPlexusSite = "https://vectorplexis.com";
    private const string DefaultInstructions = "Download the file from this page";

    public static bool TryGet(IDownloadState state, [NotNullWhen(true)] out ManualDownloadTarget? target)
    {
        switch (state)
        {
            case Manual manual:
                target = new ManualDownloadTarget(manual.Url, "Manual download",
                    string.IsNullOrWhiteSpace(manual.Prompt) ? DefaultInstructions : manual.Prompt);
                return true;
            case MediaFire mediaFire:
                target = new ManualDownloadTarget(mediaFire.Url, "MediaFire", DefaultInstructions);
                return true;
            case Mega mega:
                target = new ManualDownloadTarget(mega.Url, "Mega", DefaultInstructions);
                return true;
            case ModDB modDb:
                target = new ManualDownloadTarget(modDb.Url, "ModDB", DefaultInstructions);
                return true;
            case Http http:
                target = new ManualDownloadTarget(http.Url, "Direct download", DefaultInstructions);
                return true;
            case WabbajackCDN cdn:
                target = new ManualDownloadTarget(cdn.Url, "Wabbajack CDN", DefaultInstructions);
                return true;
            case GoogleDrive googleDrive:
                target = new ManualDownloadTarget(
                    new Uri($"https://drive.google.com/uc?id={googleDrive.Id}&export=download"),
                    "Google Drive", DefaultInstructions);
                return true;
            case Nexus nexus:
                target = NexusTarget(nexus);
                return true;
            case LoversLab loversLab:
                target = Ips4(LoversLabSite, "Lovers Lab", loversLab);
                return true;
            case VectorPlexus vectorPlexus:
                target = Ips4(VectorPlexusSite, "Vector Plexus", vectorPlexus);
                return true;
            default:
                target = null;
                return false;
        }
    }

    /// <summary>
    ///     The file's own page on Nexus: the mod's Files tab with that file selected. The query string is
    ///     the whole of what makes it a file link rather than a mod link, so whatever carries it to the
    ///     browser has to keep it intact.
    ///     <para>
    ///         Two things can be missing from the state. <c>FileID</c> is a plain <c>long</c> whose unset
    ///         value is zero, and <c>file_id=0</c> selects nothing; and two games in the registry carry no
    ///         <c>NexusName</c>, which would build a <c>//mods/</c> URL with an empty domain. Neither is
    ///         worth dropping the link over, so each falls back to the most specific page that still
    ///         resolves, and the instructions say what the user has to do for themselves once they get
    ///         there.
    ///     </para>
    /// </summary>
    private static ManualDownloadTarget NexusTarget(Nexus nexus)
    {
        var page = $"https://www.nexusmods.com/{NexusDomain(nexus.Game)}/mods/{nexus.ModID}?tab=files";
        return nexus.FileID > 0
            ? new ManualDownloadTarget(new Uri($"{page}&file_id={nexus.FileID}"), "Nexus Mods", DefaultInstructions)
            : new ManualDownloadTarget(new Uri(page), "Nexus Mods",
                "This list doesn't record which file, so find it by name in the Files tab");
    }

    /// <summary>
    ///     A game's Nexus domain. The registry's <c>NexusName</c> where it has one; where it does not, the
    ///     game's own name stripped to letters and digits and lowercased, which is how Nexus spells the
    ///     domains of the games concerned.
    /// </summary>
    private static string NexusDomain(Game game)
    {
        var name = game.MetaData().NexusName;
        return string.IsNullOrWhiteSpace(name)
            ? new string(game.ToString().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()
            : name;
    }

    private static ManualDownloadTarget Ips4(string site, string siteName, IPS4OAuth2 state)
    {
        var url = state.IsAttachment
            ? new Uri($"{site}/applications/core/interface/file/attachment.php?id={state.IPS4Mod}")
            : new Uri($"{site}/files/file/{state.IPS4Mod}/");
        var instructions = string.IsNullOrEmpty(state.IPS4File)
            ? DefaultInstructions
            : $"Download the file named {state.IPS4File}";
        return new ManualDownloadTarget(url, siteName, instructions);
    }
}
