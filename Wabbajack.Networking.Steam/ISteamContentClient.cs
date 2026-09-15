using Wabbajack.Networking.Steam.DTOs;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Reading content out of Steam, in terms that are ours rather than SteamKit's.
///     Everything on this interface is expressed with <see cref="DepotFile" />, <see cref="AppInfo" /> and
///     plain numbers, so a caller can be tested against a stand-in without a login, a network or a
///     reference to SteamKit. The one production implementation is <see cref="SteamContentClient" />, and
///     the SteamKit-typed members it also offers stay off here on purpose: the condition SteamKit's licence
///     was accepted under is that its types do not leave this project.
/// </summary>
public interface ISteamContentClient
{
    /// <summary>The app's product info: what depots it has and what each is publishing.</summary>
    Task<AppInfo> GetAppInfo(uint appId);

    /// <summary>
    ///     The manifest a depot is publishing on a branch right now, or null when it publishes none there.
    ///     Steam exposes no history, so this answers "whatever the game ships today" and nothing else.
    /// </summary>
    Task<ulong?> GetCurrentManifestIdAsync(uint appId, uint depotId,
        string branch = SteamContentClient.PublicBranch);

    /// <summary>
    ///     Every depot of the app that publishes on a branch today, with the manifest it publishes there.
    ///     This is the answer to "what does the game ship right now", and it needs no version index at all -
    ///     which is the whole of the case where the user simply never installed a file.
    /// </summary>
    Task<IReadOnlyList<DepotManifestId>> GetCurrentDepotsAsync(uint appId,
        string branch = SteamContentClient.PublicBranch);

    /// <summary>Whether the logged in account may open this depot.</summary>
    Task<DepotAccess> CheckAccessAsync(uint appId, uint depotId, CancellationToken token);

    /// <summary>
    ///     Makes sure the account holds a licence for an app Steam gives away, asking for one when it does
    ///     not. Returns whether it now holds one.
    ///     <para>
    ///         The Creation Kit is why this exists. It is free, but it is not
    ///         <see cref="DepotEntitlement.IsFreeToDownload" />-free: an account that has never installed it
    ///         holds no package naming it, and Steam then refuses even the PICS access token, so the app
    ///         cannot be so much as described, let alone read. What the Steam client does when someone
    ///         presses Install on a free store page is ask for the licence, and the account gets a
    ///         no-cost package granting exactly that app.
    ///     </para>
    ///     <para>
    ///         This adds something to the user's Steam library, which nothing else here does, so three
    ///         things hold. It is only ever called for an app whose depots are about to be read. It is not
    ///         called when the account is merely <em>not known</em> to hold the licence - a licence list
    ///         that never arrived is an unknown, not a no, and guessing wrong here puts a package in
    ///         somebody's library. And the answer is remembered for the run, negative as well as positive,
    ///         so a repair of fifty files asks once.
    ///     </para>
    ///     <para>
    ///         A false is advisory rather than a reason to stop: the account may hold the licence and the
    ///         scan simply could not confirm it, and Steam's own refusal from the depot call is the one
    ///         worth showing the user.
    ///     </para>
    /// </summary>
    Task<bool> EnsureFreeLicenseAsync(uint appId, CancellationToken token);

    /// <summary>As <see cref="CheckAccessAsync" />, but throws rather than returning an answer.</summary>
    Task EnsureAccessAsync(uint appId, uint depotId, CancellationToken token);

    /// <summary>Every file a manifest lists, without downloading any of them.</summary>
    Task<IReadOnlyList<DepotFile>> ListFilesAsync(uint appId, uint depotId, ulong manifestId,
        CancellationToken token, string branch = SteamContentClient.PublicBranch);

    /// <summary>
    ///     The manifest entry <paramref name="depotPath" /> names, or null when this manifest does not carry
    ///     it. Reads the manifest and nothing else, so a caller looking for one file across several depots
    ///     pays for the search rather than for a download it is not going to want.
    /// </summary>
    Task<DepotFile?> FindFileAsync(uint appId, uint depotId, ulong manifestId, string depotPath,
        CancellationToken token, string branch = SteamContentClient.PublicBranch);

    /// <summary>
    ///     Fetches one named file out of a depot and writes it to <paramref name="output" />, verified
    ///     against the hash the manifest carries for it. Nothing lands at <paramref name="output" /> until
    ///     that hash matches.
    /// </summary>
    Task<DepotFile> DownloadFileAsync(uint appId, uint depotId, ulong manifestId, string depotPath,
        AbsolutePath output, CancellationToken token, IJob? parentJob = null,
        string branch = SteamContentClient.PublicBranch);
}
