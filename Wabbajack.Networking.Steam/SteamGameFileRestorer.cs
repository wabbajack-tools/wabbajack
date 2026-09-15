using Microsoft.Extensions.Logging;
using SteamKit2;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Fetches a game's own files out of Steam's depots.
///     This is the Steam side of <see cref="IGameFileRestorer" />, and the only place that turns "this file
///     of this game at this version" into an app, a depot and a manifest:
///     <list type="bullet">
///         <item>
///             With no version asked for, the depots the app publishes on the public branch today. That is
///             the file the user never installed - a DLC, the Creation Kit - and it needs no index at all.
///         </item>
///         <item>
///             With a version, the depot and manifest pairs <c>indexed-game-files</c> recorded for it.
///             Steam's client API cannot enumerate a depot's history, so an older build is only reachable
///             through an id somebody wrote down while it was current.
///         </item>
///     </list>
///     A game file names one file and not which depot holds it, so each candidate manifest is searched in
///     turn and the first that carries the file is the one fetched from. Searching costs a manifest
///     download, which <see cref="SteamContentClient" /> remembers, so a repair of several files out of one
///     build pays for each manifest once.
///     <para>
///         Nor does it name which <em>app</em>. The Creation Kit is a Steam app of its own with its own
///         depots, but its <c>installdir</c> is the game's, so its files land in the game folder and a
///         modlist records them as the game's own - <c>CreationKit.exe</c>, <c>Data\Scripts.zip</c>,
///         <c>Papyrus Compiler\PapyrusCompiler.exe</c>. So the game's own depots are searched and then
///         <see cref="GameMetaData.SteamToolIDs" />, which is the only place the difference shows.
///     </para>
///     <para>
///         One app at a time, searched before the next is so much as resolved. A companion app costs a
///         licence added to the user's Steam library, so reaching one has to mean the game's own depots
///         have already been searched and did not have the file - a list missing only a DLC must not put
///         the Creation Kit in anybody's library.
///     </para>
/// </summary>
public class SteamGameFileRestorer : IGameFileRestorer
{
    private readonly ISteamContentClient _content;
    private readonly ISteamManifestIndex _index;
    private readonly ILogger<SteamGameFileRestorer> _logger;
    private readonly ISteamSession _session;

    public SteamGameFileRestorer(ILogger<SteamGameFileRestorer> logger, ISteamSession session,
        ISteamContentClient content, ISteamManifestIndex index)
    {
        _logger = logger;
        _session = session;
        _content = content;
        _index = index;
    }

    public string SourceName => "Steam";

    public GameFileRestorerStatus Status()
    {
        if (_session.IsLoggedIn)
            return new GameFileRestorerStatus(true, $"Logged into Steam as {_session.AccountName}");

        if (_session.HaveStoredToken)
            return new GameFileRestorerStatus(true, "A saved Steam login is ready to use");

        return new GameFileRestorerStatus(false,
            "Log into Steam and Wabbajack can fetch the game files this list needs from the same depots " +
            "Steam installs them from, and put them in your downloads folder. Your game install is never " +
            "written to, and nothing is fetched until you ask for it.");
    }

    /// <summary>
    ///     Names the free companion apps a repair of these games could reach, and says what reaching one
    ///     does to the user's library. Nothing at all for the ordinary case - a game the account owns, whose
    ///     depots are read with the licence the user already has.
    ///     <para>
    ///         <see cref="GameMetaData.SteamToolIDs" /> is the condition, and deliberately not
    ///         <see cref="GameMetaData.SteamIDs" />: the latter is the game's own app, which the account
    ///         already holds and <c>GameLocator</c> walks looking for an install. A Creation Kit is a
    ///         separate app that installs into the game's folder, so <c>SteamToolIDs</c> is the only place
    ///         the difference shows.
    ///     </para>
    ///     <para>
    ///         Per game rather than per file, which is a limit rather than an oversight: whether a companion
    ///         app is actually read is not known until the game's own depots have been searched and found
    ///         wanting, and that is after the licence would have been taken. So the answer is "one is in
    ///         reach of these files", and <see cref="FreeLicenseApps.Describe" /> words it as such.
    ///     </para>
    /// </summary>
    public IReadOnlyList<string> Consequences(IEnumerable<Game> games)
    {
        return FreeLicenseApps.Describe(games
            .Distinct()
            .SelectMany(game => game.MetaData().SteamToolIDs
                .Where(id => id > 0)
                .Select(id => FreeLicenseApps.Name((uint) id, game.MetaData().HumanFriendlyGameName))));
    }

    public async Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile,
        AbsolutePath output, CancellationToken token)
    {
        var appId = game.MetaData().SteamIDs.FirstOrDefault();
        if (appId <= 0)
            return new GameFileRestoreResult(GameFileRestoreOutcome.NoSource, null,
                $"{game.MetaData().HumanFriendlyGameName} is not sold on Steam, so its files cannot be fetched " +
                "from a depot.");

        if (!_session.IsLoggedIn)
        {
            var status = Status();
            if (!status.Ready) return new GameFileRestoreResult(GameFileRestoreOutcome.NotReady, null, status.Reason);

            try
            {
                await _session.LoginWithStoredTokenAsync(token);
            }
            catch (SteamLoginRequiredException ex)
            {
                return new GameFileRestoreResult(GameFileRestoreOutcome.NotReady, null, ex.Message);
            }
        }

        var wanted = gameFile.ToString();
        var asked = string.IsNullOrWhiteSpace(version) ? null : version;

        // The game's own Steam apps, all of them. The guard below is "is this the game or a companion",
        // and four games carry two SteamIDs, so asking whether the app is the first one would call the
        // second a companion and offer to add a licence for a game the user bought.
        var ownApps = game.MetaData().SteamIDs.Where(id => id > 0).Select(id => (uint) id).ToHashSet();

        // What the game itself refused, kept apart from what a companion refused. The game's answer is
        // the one the user can act on - an account that does not hold Skyrim Special Edition should be
        // told about app 489830, not sent after a free tool it never needed - so it outranks, and neither
        // is allowed to shadow the other by arriving first.
        Exception? gameRefusal = null;
        Exception? toolRefusal = null;
        var searched = 0;

        foreach (var app in AppsToSearch(game, (uint) appId, ownApps, asked))
        {
            token.ThrowIfCancellationRequested();

            var isTool = !ownApps.Contains(app);

            IReadOnlyList<DepotManifestId> candidates;
            try
            {
                // Here, and only here. A companion app is free but not free of a licence: an account that
                // never installed the Creation Kit holds no package naming it, and Steam then refuses even
                // the PICS token. Asking adds a package to their library, so it happens at the point the
                // app is about to be read - which for a tool means the game's own depots have already been
                // searched and did not carry the file. A repair that never needed the Kit never asks.
                // The answer is advisory: a false may only mean the licence scan could not confirm what
                // the account already owns, and Steam's own refusal below says more than we could.
                if (isTool) await _content.EnsureFreeLicenseAsync(app, token);

                candidates = asked == null
                    ? await _content.GetCurrentDepotsAsync(app)
                    : (await _index.Get(game, asked, token))
                    .Select(m => new DepotManifestId(m.Depot, m.Manifest)).ToArray();
            }
            catch (Exception ex) when (isTool && ex is not OperationCanceledException)
            {
                // Only a companion app, and whatever Steam's reason - a licence it would not grant, an app
                // it will not describe without one - the game's own depots hold everything except the
                // tool's own files. So this is kept in case nothing else answers, rather than ending the
                // restore of a file that was never going to come from here.
                _logger.LogInformation("App {App} cannot be searched: {Message}", app, ex.Message);
                toolRefusal ??= ex;
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Failed(ex, asked);
            }

            searched += candidates.Count;

            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    var found = await _content.FindFileAsync(app, candidate.DepotId, candidate.ManifestId,
                        wanted, token);
                    if (found == null) continue;

                    _logger.LogInformation(
                        "{File} is in app {App} depot {Depot} manifest {Manifest} as {Path} ({Size} bytes)",
                        wanted, app, candidate.DepotId, candidate.ManifestId, found.Path, found.Size);

                    await _content.DownloadFileAsync(app, candidate.DepotId, candidate.ManifestId, found.Path,
                        output, token);

                    return new GameFileRestoreResult(GameFileRestoreOutcome.Fetched, asked,
                        $"app {app}, depot {candidate.DepotId}, manifest {candidate.ManifestId}");
                }
                catch (Exception ex) when (ex is SteamNoEntitlementException
                                               or SteamEntitlementUnconfirmedException
                                               or SteamManifestUnavailableException
                                               or SteamException {Result: EResult.AccessDenied})
                {
                    // A depot the account cannot open, or a manifest Steam has stopped serving. Neither
                    // says anything about the next candidate, and the file may well be in one of those, so
                    // the refusal is kept in case nothing else answers and continues here. A depot key
                    // Steam refuses is the same answer arriving one call later, which is how an unlicensed
                    // companion app that PICS was willing to describe turns it down.
                    _logger.LogInformation("Depot {Depot} manifest {Manifest} is not available: {Message}",
                        candidate.DepotId, candidate.ManifestId, ex.Message);

                    if (isTool) toolRefusal ??= ex;
                    else gameRefusal ??= ex;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Failed(ex, asked);
                }
            }
        }

        var refusal = gameRefusal ?? toolRefusal;
        if (refusal != null) return Failed(refusal, asked);

        if (searched == 0)
            return asked == null
                ? new GameFileRestoreResult(GameFileRestoreOutcome.NoSource, null,
                    $"Steam says app {appId} publishes nothing on its public branch, so there is nowhere to " +
                    "fetch this from.")
                : new GameFileRestoreResult(GameFileRestoreOutcome.VersionUnknown, asked,
                    $"Wabbajack's game file index has no record of {game.MetaData().HumanFriendlyGameName} " +
                    $"{asked}, so the depot manifests that version was published as are not known. Steam " +
                    "itself will not say: it only ever publishes the current build.");

        return new GameFileRestoreResult(GameFileRestoreOutcome.FileNotFound, asked,
            $"None of the {searched} depot manifests " +
            $"{(asked == null ? "the game publishes now" : $"recorded for {asked}")} contains \"{wanted}\".");
    }

    /// <summary>
    ///     Which Steam apps could hold this file, in the order they are worth asking: the game, and then
    ///     whatever else installs into the game's folder - the Creation Kit, whose files a modlist records
    ///     as the game's own because that is where they sit. The order is what keeps a tool untouched by a
    ///     repair the game's own depots can satisfy.
    ///     <para>
    ///         Only for the current build. <c>indexed-game-files</c> records depot and manifest ids with no
    ///         app beside them, so an id out of it can only be asked for under the game's own app, and
    ///         asking the index once per app would fetch the same answer again and search it twice. It
    ///         costs nothing: a missing file tries the current build first and a mismatched one falls back
    ///         to it, so the tools are reached either way.
    ///     </para>
    /// </summary>
    private static IEnumerable<uint> AppsToSearch(Game game, uint appId, IReadOnlySet<uint> ownApps,
        string? version)
    {
        yield return appId;

        if (version != null) yield break;

        foreach (var tool in game.MetaData().SteamToolIDs.Where(id => id > 0).Select(id => (uint) id))
            if (!ownApps.Contains(tool))
                yield return tool;
    }

    private GameFileRestoreResult Failed(Exception ex, string? version)
    {
        _logger.LogWarning(ex, "Fetching a game file from Steam failed");
        return new GameFileRestoreResult(GameFileRestoreOutcome.Failed, version, ex.Message);
    }
}
