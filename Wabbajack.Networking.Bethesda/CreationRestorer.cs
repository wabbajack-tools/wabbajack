using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     Fetches Anniversary Edition Creations from Bethesda, using the running Steam client as the
///     credential.
///     <para>
///         This is the Bethesda side of <see cref="IGameFileRestorer" />, and it exists because Steam's
///         depots cannot answer for these files. Skyrim Special Edition publishes seventy-four Creations,
///         and depot 489831 carries four of them; the other seventy are a runtime download from
///         <c>api.bethesda.net</c> and are in no depot at all. So a list that wants one of those seventy
///         fails a depot search however well the depot path works, and <c>SteamGameFileRestorer</c> is not
///         wrong about that - it is being asked a question Steam has no answer to.
///     </para>
///     <para>
///         The thing worth knowing about this path is what it does <em>not</em> need. There is no Bethesda
///         account and no Wabbajack Steam login: the Steam client the user is already running mints an
///         encrypted app ticket stating who the account is and what it owns, Bethesda decrypts it with a key
///         only they hold, and entitlement is decided there. Everything this needs is therefore already
///         true for a user who has Skyrim installed and Steam open, which is nearly all of them.
///     </para>
///     <para>
///         Skyrim Special Edition only, deliberately. The table of Creations, the content ids and the whole
///         chain were confirmed against that game and nothing else, so every other game gets
///         <see cref="GameFileRestoreOutcome.NoSource" /> rather than a guess. A second game would be a
///         second table.
///     </para>
///     <para>
///         <c>GameFileSource</c> archives only. The <c>Bethesda</c> <em>download state</em> is a different
///         thing with its own ids, and it keeps failing <c>UnsupportedArchivesCheck</c> exactly as it does
///         today; nothing here touches it.
///     </para>
/// </summary>
public class CreationRestorer : IGameFileRestorer
{
    /// <summary>The one game whose Creations this knows about.</summary>
    public const Game SupportedGame = Game.SkyrimSpecialEdition;

    private readonly CreationCache _cache;
    private readonly CreationIndex _index;
    private readonly IGameLocator _locator;
    private readonly ILogger<CreationRestorer> _logger;
    private readonly ISteamClientPresence _steam;

    public CreationRestorer(ILogger<CreationRestorer> logger, CreationIndex index, CreationCache cache,
        IGameLocator locator, ISteamClientPresence steam)
    {
        _logger = logger;
        _index = index;
        _cache = cache;
        _locator = locator;
        _steam = steam;
    }

    public string SourceName => "Bethesda";

    /// <summary>
    ///     Whether a fetch could happen right now. Two things, both of which the user can see for
    ///     themselves: Skyrim Special Edition has to be installed - its own folder is where the library that
    ///     mints the ticket lives - and the Steam client has to be running, because the ticket comes from
    ///     the client rather than from anything Wabbajack stores.
    ///     <para>
    ///         Deliberately not a Wabbajack Steam login. That is the point of this path and the reason the
    ///         wording says so: a user who has never logged into Steam through Wabbajack, and does not want
    ///         to, can still fetch every Creation they own.
    ///     </para>
    /// </summary>
    public GameFileRestorerStatus Status()
    {
        if (!_locator.TryFindLocation(SupportedGame, out _))
            return new GameFileRestorerStatus(false,
                "Install Skyrim Special Edition through Steam and Wabbajack can fetch the Anniversary Edition " +
                "Creations this list needs. They come from Bethesda rather than from a Steam depot, and the " +
                "game's own folder is where the code that asks for them lives.");

        if (!_steam.IsRunning)
            return new GameFileRestorerStatus(false,
                "Start Steam and sign in, and Wabbajack can fetch the Anniversary Edition Creations this list " +
                "needs straight from Bethesda. This does not need a Wabbajack Steam login - the running Steam " +
                "client vouches for your account by itself - and nothing is written to your game install.");

        return new GameFileRestorerStatus(true,
            "Steam is running, so Bethesda's Creations can be fetched with no Wabbajack login");
    }

    /// <summary>
    ///     Never a stand-in for a missing install, whatever the account owns. Creations are add-ons to a
    ///     game rather than the game, so a machine with no Skyrim Special Edition folder is missing files
    ///     nothing here publishes - and the ticket that asks Bethesda for a Creation is minted by a library
    ///     that lives in that same folder, so this source does not work at all without it.
    /// </summary>
    public Task<GameSourceResult> CanSourceGame(Game game, CancellationToken token)
    {
        return Task.FromResult(new GameSourceResult(GameSourceOutcome.NoSource,
            "Bethesda publishes Creations for Skyrim Special Edition, not the game itself, so they are no " +
            "substitute for having it installed."));
    }

    /// <summary>
    ///     What fetching Creations for these games asks of the user, said only when one of them actually has
    ///     Creations to fetch - which today means Skyrim Special Edition and nothing else. A repair that
    ///     reaches none of this adds nothing to say, and a warning shown every time is a warning nobody
    ///     reads.
    ///     <para>
    ///         Per game rather than per file, the same limit <c>SteamGameFileRestorer</c> has: whether a
    ///         Creation is actually reached is not known until the fetch is under way. So the wording is
    ///         about what will be needed if one turns out to be, and the ownership sentence is there because
    ///         Bethesda decides that server-side from the Steam ticket - an account without the Anniversary
    ///         Upgrade is refused there, and there is nothing Wabbajack can check first.
    ///     </para>
    /// </summary>
    public IReadOnlyList<string> Consequences(IEnumerable<Game> games)
    {
        if (!games.Any(g => g == SupportedGame)) return Array.Empty<string>();

        return new[]
        {
            "Some of these files are Anniversary Edition Creations, which Steam does not publish as game " +
            "files. Wabbajack fetches those from Bethesda using the Steam client that is already running, so " +
            "Steam has to be open and signed in to the account that owns the Anniversary Upgrade. No " +
            "Wabbajack Steam login is needed and nothing is added to your Steam library."
        };
    }

    /// <summary>
    ///     Writes one file out of a Creation.
    ///     <para>
    ///         <paramref name="version" /> is read and not honoured, which is not a shortcut: Creations are
    ///         not published per game build. Bethesda serves one current copy of each, with no history to
    ///         ask for, so there is no sense in which one of them exists at Skyrim 1.6.1170 and not at
    ///         1.5.97. Pretending otherwise would mean answering
    ///         <see cref="GameFileRestoreOutcome.VersionUnknown" /> to a question the source does not have -
    ///         and <c>GameFileRepair</c> ranks that as its most informative failure, so the user would be
    ///         told to go and find a version index for something that has no versions. The result therefore
    ///         reports no version at all, which reads as "whatever is published now", because that is what
    ///         it is. Whether the copy published now is the one this list wants is decided where it is
    ///         decided for every other game file: by <c>Archive.Hash</c>, on the bytes that come out.
    ///     </para>
    ///     <para>
    ///         <paramref name="expectedSize" /> is not used, and there is nothing to gain by using it: a
    ///         Creation is fetched as one <c>.ckm</c> that carries both of its files, so by the time either
    ///         file's size is known the download that would have been skipped has already happened.
    ///         <c>Archive.Hash</c> still decides whether what comes out is what the list wanted.
    ///     </para>
    /// </summary>
    public async Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile,
        AbsolutePath output, CancellationToken token, long? expectedSize = null)
    {
        if (game != SupportedGame)
            return new GameFileRestoreResult(GameFileRestoreOutcome.NoSource, null,
                $"Wabbajack only knows which Creations {SupportedGame.MetaData().HumanFriendlyGameName} " +
                $"publishes, so it has nothing to fetch for {game.MetaData().HumanFriendlyGameName}.");

        var creation = _index.Find(gameFile);
        if (creation == null)
            return new GameFileRestoreResult(GameFileRestoreOutcome.FileNotFound, null,
                $"\"{gameFile}\" is not one of the {CreationIndex.ExpectedCount} Anniversary Edition Creations, " +
                "so Bethesda has nothing to serve for it.");

        // Asked after the two questions above so that a machine with no Steam running is not told to start
        // it over a file Bethesda was never going to have.
        var status = Status();
        if (!status.Ready) return new GameFileRestoreResult(GameFileRestoreOutcome.NotReady, null, status.Reason);

        try
        {
            var unpacked = await _cache.Get(creation.ContentId, token);

            if (unpacked == null)
                return new GameFileRestoreResult(GameFileRestoreOutcome.FileNotFound, null,
                    $"Bethesda returned no download for {creation.DisplayName} (content id " +
                    $"{creation.ContentId}). The account signed in to Steam may not own it - most Creations " +
                    "come with the Anniversary Upgrade - or Bethesda may have stopped publishing it.");

            var source = unpacked.Find(gameFile);
            if (source == null)
                return new GameFileRestoreResult(GameFileRestoreOutcome.FileNotFound, null,
                    $"{creation.DisplayName} was fetched and unpacked, and it contains {unpacked.Describe()} " +
                    $"rather than \"{gameFile}\".");

            output.Parent.CreateDirectory();
            await source.Value.CopyToAsync(output, token);

            _logger.LogInformation("Wrote {File} out of Creation {Creation} ({ContentId})", gameFile,
                creation.DisplayName, creation.ContentId);

            return new GameFileRestoreResult(GameFileRestoreOutcome.Fetched, null,
                $"{creation.DisplayName}, content id {creation.ContentId}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SteamAppTicketException ex)
        {
            // Every one of these is something the user can go and do - start Steam, sign in, verify the
            // game's files - and the exception already carries that sentence. NotReady rather than Failed
            // because none of it is about this file, and a run that says "not set up" is one the user can
            // set up and re-run.
            _logger.LogWarning(ex, "No Steam app ticket, so Bethesda's Creations are out of reach");
            return new GameFileRestoreResult(GameFileRestoreOutcome.NotReady, null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fetching Creation {ContentId} from Bethesda failed", creation.ContentId);
            return new GameFileRestoreResult(GameFileRestoreOutcome.Failed, null, ex.Message);
        }
    }
}
