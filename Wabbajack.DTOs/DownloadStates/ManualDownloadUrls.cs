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
                // The CDN address is a definition and numbered parts, not a file, so a browser opening it downloads
                // nothing the user could hand back. A file a list author uploaded has a download page on the build
                // server that puts the parts back together in the browser; anything else on the CDN has no page, and
                // a CDN file that will not download is then a failed download with a retry.
                target = AuthoredFilePage(cdn.Url) is { } page
                    ? new ManualDownloadTarget(page, "Wabbajack CDN", DefaultInstructions)
                    : null;
                return target != null;
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
    /// <summary>
    ///     The build server's download page for an authored file, from its CDN address: the file's id is the first
    ///     path segment on authored-files.wabbajack.org, or on wabbajack.b-cdn.net, the name it had before.
    /// </summary>
    public static Uri? AuthoredFilePage(Uri cdnUrl)
    {
        if (!cdnUrl.Host.Equals("authored-files.wabbajack.org", StringComparison.OrdinalIgnoreCase) &&
            !cdnUrl.Host.Equals("wabbajack.b-cdn.net", StringComparison.OrdinalIgnoreCase))
            return null;

        var id = cdnUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(id) ? null : new Uri($"https://build.wabbajack.org/authored_files/download/{id}");
    }

    /// <summary>Nexus Mods' download page for one file: the slow/fast choice, with the download a click away.</summary>
    public static Uri NexusDownloadPopup(long fileId, long nexusGameId)
    {
        return new Uri(
            $"https://www.nexusmods.com/Core/Libs/Common/Widgets/DownloadPopUp?id={fileId}&game_id={nexusGameId}");
    }

    private static ManualDownloadTarget NexusTarget(Nexus nexus)
    {
        var meta = nexus.Game.MetaData();

        // The page Nexus's own "Manual download" button opens: the slow/fast download choice for exactly this
        // file. Sending the user there saves finding the file on the Files tab and clicking through to it, two
        // of the three clicks a manual download used to cost. It is addressed by file id and the game's numeric
        // id rather than its domain, so a game without one keeps the Files tab link below.
        if (nexus.FileID > 0 && meta.NexusGameId > 0)
            return new ManualDownloadTarget(NexusDownloadPopup(nexus.FileID, meta.NexusGameId), "Nexus Mods",
                "Press Slow download on this page");

        var page = $"https://www.nexusmods.com/{meta.NexusDomain}/mods/{nexus.ModID}?tab=files";
        var url = nexus.FileID > 0 ? new Uri($"{page}&file_id={nexus.FileID}") : new Uri(page);

        // Where the domain came from matters to what the card can promise. With one from the registry the
        // link lands where it says it does; with one worked out from the game's name it is a good guess and
        // no more, so the card says so rather than telling the user to download from a page that may 404.
        var named = !string.IsNullOrWhiteSpace(meta.NexusName);
        var instructions = (nexus.FileID > 0, named) switch
        {
            (true, true) => DefaultInstructions,
            (false, true) => "This list doesn't record which file, so find it by name in the Files tab",
            (true, false) => "Wabbajack has no Nexus address recorded for this game, so if this page doesn't " +
                             "open, search Nexus Mods for the file by name",
            (false, false) => "Wabbajack has no Nexus address recorded for this game and this list doesn't " +
                              "record which file, so you may have to search Nexus Mods for it by name"
        };

        return new ManualDownloadTarget(url, "Nexus Mods", instructions);
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
