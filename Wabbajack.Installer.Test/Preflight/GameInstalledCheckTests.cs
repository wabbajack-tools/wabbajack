#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
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
