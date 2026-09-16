using FluentAssertions;
using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda.Steam;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

public class SteamAppGamesTests
{
    [Fact]
    public void TheAppIdTheTicketIsAskedForNamesTheGameFolderItComesFrom()
    {
        SteamAppGames.ForAppId(489830)!.Game.Should().Be(Game.SkyrimSpecialEdition);
    }

    [Fact]
    public void AToolAppIsNotAGame()
    {
        // 1946180 is Skyrim Special Edition's Creation Kit. It shares the game's folder, which is exactly why
        // it would be easy to answer with, and it is not something an account is asked to vouch for.
        SteamAppGames.ForAppId(1946180).Should().BeNull();
    }

    [Fact]
    public void AnAppIdNobodyKnowsIsNull()
    {
        SteamAppGames.ForAppId(1).Should().BeNull();
    }

    [Fact]
    public void AKnownAppIsDescribedByItsGamesName()
    {
        SteamAppGames.Describe(489830).Should()
            .Be(Game.SkyrimSpecialEdition.MetaData().HumanFriendlyGameName);
    }

    [Fact]
    public void AnUnknownAppStillSaysWhichAppItWas()
    {
        SteamAppGames.Describe(123456).Should().Be("Steam app 123456");
    }
}
