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
    ///     Writes <paramref name="gameFile" /> of <paramref name="game" />, as published at
    ///     <paramref name="version" />, to <paramref name="output" />.
    ///     A null or empty <paramref name="version" /> means whatever the game publishes now, which is the
    ///     right question for a file the user simply never installed and needs no version index to answer.
    ///     The bytes are checked against whatever hash the source itself carries for the file; the caller
    ///     still has to decide whether they are the bytes <em>it</em> wanted.
    /// </summary>
    Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile, AbsolutePath output,
        CancellationToken token);
}
