using System;
using System.Diagnostics.CodeAnalysis;

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
                target = new ManualDownloadTarget(
                    new Uri($"https://www.nexusmods.com/{nexus.Game.MetaData().NexusName}/mods/{nexus.ModID}?tab=files&file_id={nexus.FileID}"),
                    "Nexus Mods", DefaultInstructions);
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
