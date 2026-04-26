using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Wabbajack.CLI.Verbs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test;

public class ListGamesTests : IDisposable
{
    private readonly AbsolutePath _tempDir;

    public ListGamesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wj-listgames-" + Guid.NewGuid().ToString("N")[..8])
            .ToAbsolutePath();
        _tempDir.CreateDirectory();
    }

    public void Dispose()
    {
        if (_tempDir.DirectoryExists())
        {
            try { _tempDir.DeleteDirectory(); }
            catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Run_NoGamesInstalled_ReturnsZero()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.IsInstalled(Arg.Any<Game>()).Returns(false);

        var verb = new ListGames(NullLogger<ListGames>.Instance, locator);
        var result = await verb.Run(CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Run_CallsIsInstalledForAllGames()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.IsInstalled(Arg.Any<Game>()).Returns(false);

        var verb = new ListGames(NullLogger<ListGames>.Instance, locator);
        await verb.Run(CancellationToken.None);

        foreach (var game in GameRegistry.Games.Keys)
        {
            locator.Received().IsInstalled(game);
        }
    }

    [Fact]
    public async Task Run_WithInstalledGame_CallsGameLocation()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.IsInstalled(Arg.Any<Game>()).Returns(false);

        // Pick a game that has a MainExecutable defined
        var testGame = GameRegistry.Games
            .First(g => g.Value.MainExecutable != null);
        var gameKey = testGame.Key;

        locator.IsInstalled(gameKey).Returns(true);
        locator.GameLocation(gameKey).Returns(_tempDir);

        // Create a dummy main executable file so FileVersionInfo doesn't throw
        var mainExePath = testGame.Value.MainExecutable!.Value.RelativeTo(_tempDir);
        mainExePath.Parent.CreateDirectory();
        await mainExePath.WriteAllBytesAsync(new byte[] { 0x4D, 0x5A }); // MZ header stub

        var verb = new ListGames(NullLogger<ListGames>.Instance, locator);
        var result = await verb.Run(CancellationToken.None);

        Assert.Equal(0, result);
        locator.Received().GameLocation(gameKey);
    }

    [Fact]
    public async Task Run_WithMixedInstalledGames_ReturnsZero()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.IsInstalled(Arg.Any<Game>()).Returns(false);

        var verb = new ListGames(NullLogger<ListGames>.Instance, locator);
        var result = await verb.Run(CancellationToken.None);

        Assert.Equal(0, result);
        locator.DidNotReceive().GameLocation(Arg.Any<Game>());
    }
}
