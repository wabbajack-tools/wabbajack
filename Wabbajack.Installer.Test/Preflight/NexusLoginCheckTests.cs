#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class NexusLoginCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly NexusLoginCheck _check = new();
    private readonly RecordingProgress _progress = new();

    public NexusLoginCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private async Task WithNexusArchives()
    {
        _host.Config.ModList.Archives = new[]
        {
            await PreflightTestHost.ArchiveFor("one.7z", "nexus one",
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 1}),
            await PreflightTestHost.ArchiveFor("two.7z", "nexus two!",
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 2, FileID = 2}),
            await PreflightTestHost.ArchiveFor("http.7z", "plain http")
        };
    }

    [Fact]
    public async Task PassesWithoutProbingWhenTheListHasNoNexusFiles()
    {
        _host.Config.ModList.Archives = new[] {await PreflightTestHost.ArchiveFor("http.7z", "plain http")};
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(0, _host.Nexus.Calls);
        Assert.Null(ctx.State.Nexus);
    }

    [Fact]
    public async Task NoTokenNeedsTheUserToLogIn()
    {
        await WithNexusArchives();
        _host.Nexus.Status = new NexusLoginStatus(false, false, false, null, null);
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.Contains("Log in to Nexus Mods to download 2 files", result.Message);
        Assert.Contains(PreflightAction.Login, result.Actions!);
        Assert.Equal(1, _host.Nexus.Calls);
        Assert.False(ctx.State.Nexus!.HasToken);
    }

    [Fact]
    public async Task AnExpiredTokenNeedsTheUserToLogInAgain()
    {
        await WithNexusArchives();
        _host.Nexus.Status = new NexusLoginStatus(true, false, false, null, "Http Error 401 - Unauthorized");
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.Contains("expired", result.Message);
        Assert.Equal("Http Error 401 - Unauthorized", result.Detail);
        Assert.Contains(PreflightAction.Login, result.Actions!);
    }

    [Fact]
    public async Task APremiumAccountPasses()
    {
        await WithNexusArchives();
        _host.Nexus.Status = new NexusLoginStatus(true, true, true, "someone", null);
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal("Logged in as someone (Premium)", result.Message);
        Assert.True(ctx.State.Nexus!.IsPremium);
    }

    [Fact]
    public async Task AFreeAccountPassesAndSaysTheFilesWillBeManual()
    {
        await WithNexusArchives();
        _host.Nexus.Status = new NexusLoginStatus(true, true, false, "someone", null);
        var ctx = _host.Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("Logged in as someone", result.Message);
        Assert.Contains("2 Nexus files will be downloaded manually", result.Message);
        Assert.False(ctx.State.Nexus!.IsPremium);
        Assert.True(ctx.State.Nexus.LoggedIn);
    }
}
