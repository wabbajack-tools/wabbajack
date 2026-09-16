using System;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

public class SteamAppTicketMinterTests
{
    private const Game TheGame = Game.SkyrimSpecialEdition;

    [Fact]
    public void AFolderWithNoLibraryIsSaidSoRatherThanLoaded()
    {
        var thrown = Assert.Throws<SteamAppTicketException>(() =>
            SteamAppTicketMinter.Mint(KnownFolders.EntryPoint.Combine("no-game-here"), 489830,
                "Skyrim Special Edition", TimeSpan.FromSeconds(1), NullLogger.Instance, CancellationToken.None));

        Assert.Equal(SteamAppTicketError.SteamApiMissing, thrown.Error);
        Assert.Contains(SteamAppTicketMinter.LibraryName, thrown.Message);
    }

    /// <summary>
    ///     The real thing, against the Steam client on this machine. Traited out of the offline lane because
    ///     there is no way to fake the other side of it: the ticket is minted inside Steam.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public void ARunningSteamClientMintsATicket()
    {
        var locator = new GameLocator(NullLogger<GameLocator>.Instance);
        Assert.True(locator.TryFindLocation(TheGame, out var folder),
            "this test is traited for a machine that has Steam, the game, and a signed-in account");

        var ticket = SteamAppTicketMinter.Mint(folder, (uint) TheGame.MetaData().SteamIDs[0],
            TheGame.MetaData().HumanFriendlyGameName, TimeSpan.FromSeconds(30), NullLogger.Instance,
            CancellationToken.None);

        // Around 159 bytes in practice; the bound is deliberately loose, since the contents are Valve's.
        Assert.InRange(ticket.Length, 64, SteamApiNativeTicketBound);

        Assert.True(SteamAppTicketOutput.TryParse(SteamAppTicketOutput.Format(ticket), out var round));
        Assert.Equal(ticket, round);
    }

    private const int SteamApiNativeTicketBound = 2048;
}
