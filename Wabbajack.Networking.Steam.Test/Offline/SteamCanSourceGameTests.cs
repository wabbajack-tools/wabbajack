using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Xunit;

namespace Wabbajack.Networking.Steam.Test.Offline;

/// <summary>
///     Whether a Steam account can stand in for a game that is not installed. This is what turns preflight's
///     "game not found" failure into a warning a user can carry on past, so each answer has to be the true
///     one: telling an owner to go and buy their game, or telling a non-owner the files are on their way,
///     are both regressions a user would hit at the first row of the checklist.
/// </summary>
public class SteamCanSourceGameTests
{
    private const uint SkyrimSE = 489830;

    private readonly FakeContentClient _content = new();
    private readonly FakeIndex _index = new();
    private readonly FakeSession _session = new();

    private SteamGameFileRestorer Restorer()
    {
        return new SteamGameFileRestorer(NullLogger<SteamGameFileRestorer>.Instance, _session, _content, _index);
    }

    private Task<GameSourceResult> Ask(Game game = Game.SkyrimSpecialEdition)
    {
        return Restorer().CanSourceGame(game, CancellationToken.None);
    }

    /// <summary>A game Steam does not sell is answered without logging in or asking Steam anything.</summary>
    [Fact]
    public async Task AGameThatIsNotOnSteamHasNoSource()
    {
        _session.IsLoggedIn = true;

        var result = await Ask(Game.Fallout4London);

        Assert.Equal(GameSourceOutcome.NoSource, result.Outcome);
        Assert.Contains("not sold on Steam", result.Reason);
        Assert.Empty(_content.AccessChecked);
    }

    /// <summary>
    ///     Nobody is prompted from preflight. With no login and nothing saved the answer is "not ready", and
    ///     the reason is the same sentence the Status row uses, saying what a login would buy.
    /// </summary>
    [Fact]
    public async Task WithNoLoginAndNothingSavedItIsNotReadyAndSaysWhatALoginWouldBuy()
    {
        var result = await Ask();

        Assert.Equal(GameSourceOutcome.NotReady, result.Outcome);
        Assert.Equal(Restorer().Status().Reason, result.Reason);
        Assert.Contains("Log into Steam", result.Reason);
        Assert.Equal(0, _session.StoredLoginAttempts);
        Assert.Empty(_content.AccessChecked);
    }

    /// <summary>A login the user already saved is used, so they are not asked to log in again.</summary>
    [Fact]
    public async Task ASavedLoginIsRedeemedBeforeOwnershipIsAsked()
    {
        _session.HaveStoredToken = true;

        var result = await Ask();

        Assert.Equal(GameSourceOutcome.Available, result.Outcome);
        Assert.True(_session.LoggedInWithStoredToken);
        Assert.Equal((SkyrimSE, SkyrimSE), Assert.Single(_content.AccessChecked));
    }

    /// <summary>
    ///     A saved login that Steam no longer accepts is a login the user has to make again, and the
    ///     session's own sentence says so. It must not be reported as "you do not own the game".
    /// </summary>
    [Fact]
    public async Task ASavedLoginThatNoLongerWorksIsNotReadyWithItsOwnMessage()
    {
        _session.HaveStoredToken = true;
        _session.StoredLoginThrows = () => new SteamLoginRequiredException("Your saved Steam login has expired.");

        var result = await Ask();

        Assert.Equal(GameSourceOutcome.NotReady, result.Outcome);
        Assert.Equal("Your saved Steam login has expired.", result.Reason);
        Assert.Empty(_content.AccessChecked);
    }

    /// <summary>Ownership is asked of the game's own app id, as both the app and the depot.</summary>
    [Fact]
    public async Task AnAccountThatOwnsTheGameCanStandInForTheInstall()
    {
        _session.IsLoggedIn = true;

        var result = await Ask();

        Assert.Equal(GameSourceOutcome.Available, result.Outcome);
        Assert.Contains("owns Skyrim Special Edition", result.Reason);
        Assert.Equal((SkyrimSE, SkyrimSE), Assert.Single(_content.AccessChecked));
    }

    /// <summary>
    ///     An account that does not own the game is told which account and which app, so a user logged in
    ///     with the wrong Steam account can see that straight away.
    /// </summary>
    [Fact]
    public async Task AnAccountThatDoesNotOwnTheGameIsToldWhichAccountAndWhichApp()
    {
        _session.IsLoggedIn = true;
        _session.AccountName = "alt_account";
        _content.Refuses.Add(SkyrimSE);

        var result = await Ask();

        Assert.Equal(GameSourceOutcome.NotOwned, result.Outcome);
        Assert.Contains("alt_account", result.Reason);
        Assert.Contains("app 489830", result.Reason);
        Assert.Contains("holds no licence", result.Reason);
    }

    /// <summary>
    ///     A licence list that never arrived is not an account that owns nothing. The answer stays
    ///     "unconfirmed" and points at the connection, so an owner is never told to buy their own game.
    /// </summary>
    [Fact]
    public async Task ALicenceListThatNeverArrivedIsUnconfirmedNotNotOwned()
    {
        _session.IsLoggedIn = true;
        _content.Unconfirmed.Add(SkyrimSE);

        var result = await Ask();

        Assert.Equal(GameSourceOutcome.Unconfirmed, result.Outcome);
        Assert.Contains("Steam did not say what the account tester owns", result.Reason);
        Assert.Contains("Check your connection", result.Reason);
        Assert.DoesNotContain("holds no licence", result.Reason);
    }

    /// <summary>A Steam call that breaks is also an unknown, not a "no", and carries what went wrong.</summary>
    [Fact]
    public async Task AFailureAskingSteamIsUnconfirmedAndSaysWhy()
    {
        _session.IsLoggedIn = true;
        _content.CheckAccessThrows = () => new IOException("the connection dropped");

        var result = await Ask();

        Assert.Equal(GameSourceOutcome.Unconfirmed, result.Outcome);
        Assert.Contains("could not be asked", result.Reason);
        Assert.Contains("the connection dropped", result.Reason);
    }

    /// <summary>A user who cancels gets a cancellation, not an "unconfirmed" row they then have to dismiss.</summary>
    [Fact]
    public async Task CancellingWhileSteamIsBeingAskedIsACancellation()
    {
        _session.IsLoggedIn = true;
        _content.CheckAccessThrows = () => new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Ask());
    }
}
