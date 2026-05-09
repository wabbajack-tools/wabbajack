// Wabbajack.Test/Preflight/GameInstalledCheckTests.cs
using NSubstitute;
using Wabbajack.DTOs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Paths;
using Wabbajack.Preflight;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class GameInstalledCheckTests
{
    [Fact]
    public void GameInstalled_Passes()
    {
        var locator = Substitute.For<IGameLocator>();
        AbsolutePath path = (AbsolutePath)@"C:\Games\Skyrim";
        locator.TryFindLocation(Game.SkyrimSpecialEdition, out Arg.Any<AbsolutePath>())
            .Returns(x => { x[1] = path; return true; });

        var check = new GameInstalledCheck(locator, Game.SkyrimSpecialEdition);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void GameNotInstalled_Fails()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.TryFindLocation(Game.SkyrimSpecialEdition, out Arg.Any<AbsolutePath>())
            .Returns(false);

        var check = new GameInstalledCheck(locator, Game.SkyrimSpecialEdition);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("Skyrim", check.FailureMessage);
    }
}
