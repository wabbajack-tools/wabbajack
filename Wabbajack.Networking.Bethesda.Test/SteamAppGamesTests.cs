using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda.Steam;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

public class SteamAppGamesTests
{
    [Fact]
    public void TheAppIdTheTicketIsAskedForNamesTheGameFolderItComesFrom()
    {
        Assert.Equal(Game.SkyrimSpecialEdition, SteamAppGames.ForAppId(489830)!.Game);
    }

    [Fact]
    public void AToolAppIsNotAGame()
    {
        // 1946180 is Skyrim Special Edition's Creation Kit. It shares the game's folder, which is exactly why
        // it would be easy to answer with, and it is not something an account is asked to vouch for.
        Assert.Null(SteamAppGames.ForAppId(1946180));
    }

    [Fact]
    public void AnAppIdNobodyKnowsIsNull()
    {
        Assert.Null(SteamAppGames.ForAppId(1));
    }

    [Fact]
    public void AKnownAppIsDescribedByItsGamesName()
    {
        Assert.Equal(Game.SkyrimSpecialEdition.MetaData().HumanFriendlyGameName, SteamAppGames.Describe(489830));
    }

    [Fact]
    public void AnUnknownAppStillSaysWhichAppItWas()
    {
        Assert.Equal("Steam app 123456", SteamAppGames.Describe(123456));
    }
}
