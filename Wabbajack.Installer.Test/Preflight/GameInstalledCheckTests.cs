#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class GameInstalledCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly GameInstalledCheck _check = new();
    private readonly RecordingProgress _progress = new();

    public GameInstalledCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task FindsTheGameThroughTheLocator()
    {
        var ctx = _host.Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("Skyrim Special Edition", result.Message);
        Assert.Equal(_host.GameFolder, ctx.State.GameFolder);
        Assert.Equal(_host.GameFolder, ctx.Config.GameFolder);
    }

    [Fact]
    public async Task AnOverrideMustExist()
    {
        _host.Config.GameFolder = _host.Manager.CreateFolder().Path.Combine("nope");
        var ctx = _host.Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("does not exist", result.Message);
        Assert.Contains(PreflightAction.BrowseGameFolder, result.Actions!);
    }

    [Fact]
    public async Task AnOverrideWinsOverTheLocator()
    {
        var elsewhere = _host.Manager.CreateFolder().Path;
        _host.Config.GameFolder = elsewhere;
        var ctx = _host.Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(elsewhere, ctx.State.GameFolder);
    }

    [Fact]
    public async Task NotFoundMentionsACommonlyConfusedGameThatIsInstalled()
    {
        _host.Locator.Games.Clear();
        _host.Locator.Games[Game.Skyrim] = _host.Manager.CreateFolder().Path;
        var ctx = _host.Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("Skyrim Special Edition", result.Message);
        Assert.Contains("Skyrim Legendary Edition", result.Message);
        Assert.Contains("did you install the wrong game?", result.Message);
        Assert.Contains(PreflightAction.BrowseGameFolder, result.Actions!);
    }

    [Fact]
    public async Task NotFoundWithoutAConfusedGameJustSaysSo()
    {
        _host.Locator.Games.Clear();
        var ctx = _host.Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("doesn't seem to be installed.", result.Message);
        Assert.DoesNotContain("wrong game", result.Message);
        Assert.Equal(default, ctx.Config.GameFolder);
    }

    [Fact]
    public async Task ALocatedFolderThatIsGoneFails()
    {
        _host.Locator.Games[Game.SkyrimSpecialEdition] = _host.Manager.CreateFolder().Path.Combine("moved");
        var ctx = _host.Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("does not exist", result.Message);
    }

    /// <summary>
    ///     The point of the whole thing: a game nobody installed, and a source that says the account owns it
    ///     where the files come from. The run carries on with no game folder at all, because what the
    ///     install needs from the game is its files and those can be fetched into the downloads folder.
    ///     A Warning rather than a Pass: the user has to acknowledge it, since installing a list for a game
    ///     that is not on the machine is a decision rather than an oversight.
    /// </summary>
    [Fact]
    public async Task ANotInstalledGameThatSteamCanSupplyIsAWarning()
    {
        _host.Locator.Games.Clear();
        _host.Restorer = Sourcing(GameSourceOutcome.Available, "Your Steam account owns Skyrim Special Edition.");
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Warning, result.State);
        Assert.Contains("not installed", result.Message);
        Assert.Contains("Steam", result.Message);
        Assert.Contains("Your Steam account owns", result.Detail);
        Assert.Equal(default, ctx.State.GameFolder);
        Assert.Equal(default, ctx.Config.GameFolder);
        Assert.Contains(Game.SkyrimSpecialEdition, ctx.State.SourcedGames);

        // A Warning that cannot be accepted leaves the run at not-ready for ever, and Locate game is still
        // the right answer for somebody whose install is somewhere the locator does not look.
        Assert.Contains(PreflightAction.ContinueAnyway, result.Actions!);
        Assert.Contains(PreflightAction.BrowseGameFolder, result.Actions!);
    }

    /// <summary>
    ///     Every answer that is not "the account owns it" leaves the run stopped exactly as it was before,
    ///     with the source's sentence underneath: not logged in, does not own it, and a store that would not
    ///     say - which is deliberately not treated as a no, but is not enough to install on either.
    /// </summary>
    [Theory]
    [InlineData(GameSourceOutcome.NotReady, "Log into Steam")]
    [InlineData(GameSourceOutcome.NotOwned, "holds no licence")]
    [InlineData(GameSourceOutcome.Unconfirmed, "did not say")]
    public async Task ASourceThatCannotSupplyTheGameLeavesTheFailure(GameSourceOutcome outcome, string reason)
    {
        _host.Locator.Games.Clear();
        _host.Restorer = Sourcing(outcome, reason);
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("doesn't seem to be installed", result.Message);
        Assert.Contains(reason, result.Detail);
        Assert.Empty(ctx.State.SourcedGames);
    }

    /// <summary>
    ///     "This source does not carry that game" is not advice, and under "your game is not installed" it
    ///     reads as a second failure rather than as the non-answer it is.
    /// </summary>
    [Fact]
    public async Task AGameNoSourceCarriesSaysNothingExtra()
    {
        _host.Locator.Games.Clear();
        _host.Restorer = Sourcing(GameSourceOutcome.NoSource, "Not sold on Steam.");
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Null(result.Detail);
    }

    /// <summary>A host with no way to fetch game files behaves exactly as it always did.</summary>
    [Fact]
    public async Task WithNoRestorerAMissingGameStillFails()
    {
        _host.Locator.Games.Clear();
        _host.Restorer = null;
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Empty(ctx.State.SourcedGames);
    }

    /// <summary>
    ///     A folder the user pointed at is their answer to this question. It is not there, so it is a
    ///     mistake to correct - nothing is fetched over the top of it, and no store is asked about it.
    /// </summary>
    [Fact]
    public async Task AnOverrideThatIsGoneIsNeverSourced()
    {
        var restorer = Sourcing(GameSourceOutcome.Available, "owned");
        _host.Restorer = restorer;
        _host.Config.GameFolder = _host.Manager.CreateFolder().Path.Combine("nope");
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Empty(restorer.AskedToSource);
    }

    /// <summary>
    ///     A game the locator does find is never asked about, so the ordinary install pays nothing for any
    ///     of this: no login, no round trip to a store.
    /// </summary>
    [Fact]
    public async Task AnInstalledGameIsNeverAskedAbout()
    {
        var restorer = Sourcing(GameSourceOutcome.Available, "owned");
        _host.Restorer = restorer;
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Empty(restorer.AskedToSource);
        Assert.Empty(ctx.State.SourcedGames);
    }

    /// <summary>
    ///     The blackboard entry is this check's, and every run rewrites it. A user who sourced a game and
    ///     then installed it must not leave game-files offering to fetch what is now on disk.
    /// </summary>
    [Fact]
    public async Task FindingTheGameClearsAnEarlierSourcingDecision()
    {
        _host.Locator.Games.Clear();
        _host.Restorer = Sourcing(GameSourceOutcome.Available, "owned");
        var ctx = _host.Context();
        await _check.Run(ctx, _progress, CancellationToken.None);
        Assert.Contains(Game.SkyrimSpecialEdition, ctx.State.SourcedGames);

        _host.Locator.Games[Game.SkyrimSpecialEdition] = _host.GameFolder;
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Empty(ctx.State.SourcedGames);
        Assert.Equal(_host.GameFolder, ctx.State.GameFolder);
    }

    /// <summary>A source that falls over answers the question it was asked, rather than taking the run down.</summary>
    [Fact]
    public async Task ASourceThatThrowsIsJustAnUnavailableOne()
    {
        _host.Locator.Games.Clear();
        _host.Restorer = new FakeGameFileRestorer {SourceThrows = () => new Exception("Steam fell over")};
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("Steam fell over", result.Detail);
    }

    private static FakeGameFileRestorer Sourcing(GameSourceOutcome outcome, string reason)
    {
        return new FakeGameFileRestorer {GameSource = new GameSourceResult(outcome, reason)};
    }

    [Fact]
    public async Task ResolvesOtherGamesWithoutFailingOnMissingOnes()
    {
        var fallout = _host.Manager.CreateFolder().Path;
        _host.Locator.Games[Game.Fallout4] = fallout;
        _host.Config.OtherGames = new[] {Game.Fallout4, Game.Oblivion};
        var ctx = _host.Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(fallout, ctx.State.OtherGameFolders[Game.Fallout4]);
        Assert.False(ctx.State.OtherGameFolders.ContainsKey(Game.Oblivion));
        Assert.Contains("Oblivion", result.Detail);
    }
}
