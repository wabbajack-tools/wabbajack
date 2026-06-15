// Wabbajack.Test/Preflight/GameInstalledCheckTests.cs
using NSubstitute;
using Wabbajack.DTOs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Paths;
using Wabbajack.Preflight;

namespace Wabbajack.Preflight.Test;

public class GameInstalledCheckTests
{
    [Test]
    public async Task GameInstalled_Passes()
    {
        var locator = Substitute.For<IGameLocator>();
        AbsolutePath path = (AbsolutePath)@"C:\Games\Skyrim";
        locator.TryFindLocation(Game.SkyrimSpecialEdition, out Arg.Any<AbsolutePath>())
            .Returns(x => { x[1] = path; return true; });

        var check = new GameInstalledCheck(locator, Game.SkyrimSpecialEdition);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Passed);
    }

    [Test]
    public async Task GameNotInstalled_Fails()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.TryFindLocation(Game.SkyrimSpecialEdition, out Arg.Any<AbsolutePath>())
            .Returns(false);

        var check = new GameInstalledCheck(locator, Game.SkyrimSpecialEdition);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Failed);
        await Assert.That(check.FailureMessage).Contains("Skyrim");
    }
}
