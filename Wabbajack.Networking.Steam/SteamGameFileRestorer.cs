using Microsoft.Extensions.Logging;
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

        IReadOnlyList<DepotManifestId> candidates;
        try
        {
            candidates = asked == null
                ? await _content.GetCurrentDepotsAsync((uint) appId)
                : (await _index.Get(game, asked, token))
                .Select(m => new DepotManifestId(m.Depot, m.Manifest)).ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(ex, asked);
        }

        if (candidates.Count == 0)
            return asked == null
                ? new GameFileRestoreResult(GameFileRestoreOutcome.NoSource, null,
                    $"Steam says app {appId} publishes nothing on its public branch, so there is nowhere to " +
                    "fetch this from.")
                : new GameFileRestoreResult(GameFileRestoreOutcome.VersionUnknown, asked,
                    $"Wabbajack's game file index has no record of {game.MetaData().HumanFriendlyGameName} " +
                    $"{asked}, so the depot manifests that version was published as are not known. Steam " +
                    "itself will not say: it only ever publishes the current build.");

        Exception? refused = null;

        foreach (var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var found = await _content.FindFileAsync((uint) appId, candidate.DepotId, candidate.ManifestId,
                    wanted, token);
                if (found == null) continue;

                _logger.LogInformation("{File} is in depot {Depot} manifest {Manifest} as {Path} ({Size} bytes)",
                    wanted, candidate.DepotId, candidate.ManifestId, found.Path, found.Size);

                await _content.DownloadFileAsync((uint) appId, candidate.DepotId, candidate.ManifestId, found.Path,
                    output, token);

                return new GameFileRestoreResult(GameFileRestoreOutcome.Fetched, asked,
                    $"depot {candidate.DepotId}, manifest {candidate.ManifestId}");
            }
            catch (Exception ex) when (ex is SteamNoEntitlementException or SteamEntitlementUnconfirmedException
                                           or SteamManifestUnavailableException)
            {
                // A depot the account cannot open, or a manifest Steam has stopped serving. Neither says
                // anything about the next candidate, and the file may well be in one of those, so the
                // refusal is kept in case nothing else answers and continues here.
                _logger.LogInformation("Depot {Depot} manifest {Manifest} is not available: {Message}",
                    candidate.DepotId, candidate.ManifestId, ex.Message);
                refused ??= ex;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Failed(ex, asked);
            }
        }

        if (refused != null) return Failed(refused, asked);

        return new GameFileRestoreResult(GameFileRestoreOutcome.FileNotFound, asked,
            $"None of the {candidates.Count} depot manifests " +
            $"{(asked == null ? "the game publishes now" : $"recorded for {asked}")} contains \"{wanted}\".");
    }

    private GameFileRestoreResult Failed(Exception ex, string? version)
    {
        _logger.LogWarning(ex, "Fetching a game file from Steam failed");
        return new GameFileRestoreResult(GameFileRestoreOutcome.Failed, version, ex.Message);
    }
}
