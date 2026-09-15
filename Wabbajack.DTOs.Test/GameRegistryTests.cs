using System.Linq;
using Xunit;

namespace Wabbajack.DTOs.Test;

/// <summary>
///     The store ids in the registry, and the rules that keep them apart.
/// </summary>
public class GameRegistryTests
{
    /// <summary>
    ///     The Creation Kit app ids, confirmed against Steam's own product info: each is a
    ///     <c>Tool</c>/<c>Application</c> whose <c>common/parent</c> is the game and whose
    ///     <c>config/installdir</c> is the game's own folder, which is why its files are recorded as the
    ///     game's.
    /// </summary>
    [Theory]
    [InlineData(Game.Skyrim, 202480)]
    [InlineData(Game.SkyrimSpecialEdition, 1946180)]
    [InlineData(Game.Fallout4, 1946160)]
    [InlineData(Game.Starfield, 2722710)]
    public void TheCreationKitsAreRecordedAgainstTheirGames(Game game, int appId)
    {
        Assert.Equal(new[] {appId}, game.MetaData().SteamToolIDs);
    }

    /// <summary>
    ///     <c>SteamIDs</c> is what <c>GameLocator</c> walks to find an install, and it walks <em>every</em>
    ///     game's, so the invariant is against all of them rather than each game's own. A companion app
    ///     shares its game's directory and has no executable worth finding, so an id in both lists would
    ///     hand the locator a second answer to the same question - and hand the Steam restorer an app it
    ///     would treat as a game somebody bought rather than a tool it may offer to add.
    /// </summary>
    [Fact]
    public void NoToolAppIsAlsoListedAsAGame()
    {
        var games = GameRegistry.Games.Values.SelectMany(m => m.SteamIDs).ToHashSet();

        foreach (var meta in GameRegistry.Games.Values)
            Assert.DoesNotContain(meta.SteamToolIDs, games.Contains);
    }

    /// <summary>
    ///     A tool belongs to one game. Two games claiming the same app would mean a file fetched for one of
    ///     them landing under the other's name.
    /// </summary>
    [Fact]
    public void NoTwoGamesClaimTheSameTool()
    {
        var tools = GameRegistry.Games.Values.SelectMany(m => m.SteamToolIDs).ToArray();

        Assert.Equal(tools.Length, tools.Distinct().Count());
    }

    /// <summary>
    ///     A tool is only reachable through the game's own Steam app, so one without a game to hang off
    ///     would never be searched.
    /// </summary>
    [Fact]
    public void EveryGameWithToolsIsItselfOnSteam()
    {
        foreach (var meta in GameRegistry.Games.Values.Where(m => m.SteamToolIDs.Any()))
            Assert.NotEmpty(meta.SteamIDs);
    }
}
