using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

/// <summary>
///     How a restore attempt ended. Everything other than <see cref="Fetched" /> is a reason worth putting
///     in front of the user: each one is a different thing to do about it, and "it didn't work" is the one
///     answer that helps nobody.
/// </summary>
public enum GameFileRestoreOutcome
{
    /// <summary>The file was written, and it hashed to what the source said it should.</summary>
    Fetched,

    /// <summary>Nothing was tried: the restorer is not set up, typically because nobody has logged in.</summary>
    NotReady,

    /// <summary>This game is not one the restorer can fetch files for at all.</summary>
    NoSource,

    /// <summary>The wanted version is not one the restorer can resolve; a newer or older build may be.</summary>
    VersionUnknown,

    /// <summary>The version resolved, and nothing published at it carries that file.</summary>
    FileNotFound,

    /// <summary>Something went wrong fetching it. <c>Detail</c> says what.</summary>
    Failed
}

/// <summary>
///     What one restore attempt did.
/// </summary>
/// <param name="Version">
///     The version the file was actually resolved at, for a caller reporting what it fetched. Not always the
///     version that was asked for: a caller may ask for whatever the game publishes now, and this is what
///     that turned out to be.
/// </param>
/// <param name="Detail">A sentence about why, for every outcome that is not <see cref="GameFileRestoreOutcome.Fetched" />.</param>
public record GameFileRestoreResult(GameFileRestoreOutcome Outcome, string? Version = null, string? Detail = null)
{
    public bool Fetched => Outcome == GameFileRestoreOutcome.Fetched;
}

/// <param name="Ready">Whether a restore would be attempted at all right now.</param>
/// <param name="Reason">
///     When it would not: what the user would have to do, and what doing it would buy them. Shown as-is, so
///     it is written for them rather than for a log.
/// </param>
public record GameFileRestorerStatus(bool Ready, string Reason);

/// <summary>
///     Whether a source could stand in for a game that is not installed on this machine at all.
///     <para>
///         The three unavailable answers are kept apart for the same reason <c>DepotAccess</c> keeps its
///         three: "log in and this works", "this account does not own the game" and "we could not find out"
///         are three different things for the user to do, and the last is not a no.
///     </para>
/// </summary>
public enum GameSourceOutcome
{
    /// <summary>The account owns the game here, so its files can be fetched without a local install.</summary>
    Available,

    /// <summary>Nothing was asked: nobody is logged into this source.</summary>
    NotReady,

    /// <summary>This source does not carry this game's own files, whoever is logged in.</summary>
    NoSource,

    /// <summary>Asked, and the account does not own the game.</summary>
    NotOwned,

    /// <summary>Asked, and the source would not say. Not a denial.</summary>
    Unconfirmed
}

/// <param name="Reason">A sentence for the user: what this means, and what they could do about it.</param>
public record GameSourceResult(GameSourceOutcome Outcome, string Reason)
{
    public bool Available => Outcome == GameSourceOutcome.Available;
}

/// <summary>
///     Fetches a file out of a game's own published content, at a named version of that game.
///     This is the seam between wanting a game file and wherever game files come from. It is deliberately
///     not "talk to Steam": the caller says which file of which game at which version, and an implementation
///     works out for itself what store, app, depot and manifest that means. That keeps the store's client
///     library - and its licence - on one side of the line, and lets the callers be tested against a
///     stand-in with no login and no network.
/// </summary>
public interface IGameFileRestorer
{
    /// <summary>Where the files come from, for a message: "Steam".</summary>
    string SourceName { get; }

    /// <summary>
    ///     Whether this could fetch anything right now. Asked before anything is offered to the user, so a
    ///     restore is never something that happens to them: an account is theirs to hand over or not.
    /// </summary>
    GameFileRestorerStatus Status();

    /// <summary>
    ///     Whether this source could supply <paramref name="game" />'s own files on a machine where the game
    ///     is not installed. Asked by preflight when it cannot find the game folder, and only then: an
    ///     install that has the game needs none of this.
    ///     <para>
    ///         A wider question than <see cref="Status" />, and a narrower one than <see cref="Restore" />.
    ///         Wider because it asks about the account as well as the login - whether the store will hand
    ///         this particular game over - and narrower because it is about the game's own published
    ///         content and not about any one file. A source that carries add-ons for a game rather than the
    ///         game answers <see cref="GameSourceOutcome.NoSource" />: those are no substitute for an
    ///         install.
    ///     </para>
    ///     <para>
    ///         It may log in with a credential the user has already given, because that is a login they
    ///         made, but it must not ask them for a new one: this runs while preflight is reporting, not
    ///         while the user is deciding.
    ///     </para>
    /// </summary>
    Task<GameSourceResult> CanSourceGame(Game game, CancellationToken token);

    /// <summary>
    ///     What repairing these games' files would do beyond downloading them, in sentences written for the
    ///     user. Empty when the answer is nothing, which is the usual one.
    ///     <para>
    ///         Separate from <see cref="Status" />, which asks whether a repair could run at all; this
    ///         depends on <em>which</em> files are being repaired. It exists because one case is not a
    ///         download at all: a store may not hand over a free tool's files until the account holds a
    ///         licence for it, and taking that licence adds the tool to the user's library. That is the only
    ///         thing here that writes to their account rather than their disk, so it has to be in front of
    ///         them while they are deciding rather than reported afterwards.
    ///     </para>
    ///     <para>
    ///         Only said when it is true. A repair that reaches nothing of the sort adds nothing, and a
    ///         warning shown every time is a warning nobody reads.
    ///     </para>
    /// </summary>
    IReadOnlyList<string> Consequences(IEnumerable<Game> games);

    /// <summary>
    ///     Writes <paramref name="gameFile" /> of <paramref name="game" />, as published at
    ///     <paramref name="version" />, to <paramref name="output" />.
    ///     A null or empty <paramref name="version" /> means whatever the game publishes now, which is the
    ///     right question for a file the user simply never installed and needs no version index to answer.
    ///     The bytes are checked against whatever hash the source itself carries for the file; the caller
    ///     still has to decide whether they are the bytes <em>it</em> wanted.
    /// </summary>
    /// <param name="expectedSize">
    ///     How big the wanted file is, when the caller knows - a modlist records it. A source that can see a
    ///     file's size before fetching it should refuse one of another size rather than download it: the
    ///     caller is going to hash what comes back and throw away anything that is not the file it asked
    ///     for, and a list built against a game version the store no longer publishes fails that check on
    ///     nearly every file. Skyrim's textures alone are several gigabytes to establish something a number
    ///     in a manifest already said. Null means the caller does not know, and nothing is pre-checked.
    /// </param>
    Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile, AbsolutePath output,
        CancellationToken token, long? expectedSize = null);
}
