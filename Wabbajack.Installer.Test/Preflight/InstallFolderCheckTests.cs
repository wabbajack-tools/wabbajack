#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class InstallFolderCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly InstallFolderCheck _check = new();
    private readonly RecordingProgress _progress = new();

    public InstallFolderCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private void Directives(params string[] destinations)
    {
        var directives = new Directive[destinations.Length];
        for (var i = 0; i < destinations.Length; i++)
            directives[i] = new InlineFile {To = destinations[i].ToRelativePath()};

        _host.Config.ModList.Directives = directives;
    }

    private Task<PreflightResult> Run() => _check.Run(_host.Context(), _progress, CancellationToken.None);

    [Fact]
    public async Task AnEmptyFolder_Passes()
    {
        var result = await Run();

        Assert.Equal(PreflightState.Passed, result.State);
    }

    [Fact]
    public async Task AFolderOfUnrelatedFiles_StopsTheRunBeforeAnythingIsDownloaded()
    {
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("Oblivion", "Oblivion.exe"), "a game");
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("photos", "wedding.jpg"), "a photo");
        Directives("mods/a/plain.txt");

        var result = await Run();

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("not part of any modlist", result.Message);
        Assert.Contains(PreflightAction.Retry, result.Actions!);
    }

    [Fact]
    public void TheCheckRunsBeforeTheDownloadChecks()
    {
        Assert.True(((IPreflightCheck) _check).NeedsUserStopsRun);
        Assert.True(_check.Order < new ArchiveInventoryCheck().Order);
        Assert.True(_check.Order < new AutomatedDownloadsCheck().Order);
        Assert.True(_check.Order < new ManualDownloadsCheck().Order);
    }

    [Fact]
    public async Task TheSameModListAlreadyInstalled_Passes()
    {
        await InstallFolderMarker.Write(_host.Config.Install, _host.Config.ModList.Name, null, "1.0.0.0");
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("mods", "old", "stale.txt"), "stale");
        Directives("mods/a/plain.txt");

        var result = await Run();

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("is already installed here", result.Message);
    }

    [Fact]
    public async Task AnotherModList_StopsTheRunToAskBeforeDownloading()
    {
        await InstallFolderMarker.Write(_host.Config.Install, "Some Other List", null, "1.0.0.0");
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("mods", "old", "stale.txt"), "stale");
        Directives("mods/a/plain.txt");

        var result = await Run();

        Assert.Equal(PreflightState.NeedsUser, result.State);
        Assert.Contains("Some Other List", result.Message);
        Assert.Contains(PreflightAction.ContinueAnyway, result.Actions!);
    }

    [Fact]
    public async Task AnMo2InstallWithoutAMarker_IsStillRecognized()
    {
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("ModOrganizer.ini"), "[General]");
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("portable.txt"), "");
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("mods", "old", "stale.txt"), "stale");
        Directives("mods/a/plain.txt");

        var result = await Run();

        Assert.Equal(PreflightState.NeedsUser, result.State);
    }

    [Fact]
    public async Task TheVerdictIsLeftOnTheBlackboard()
    {
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("taxes.xlsx"), "mine");
        Directives("mods/a/plain.txt");

        var ctx = _host.Context();
        await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.NotNull(ctx.State.InstallFolder);
        Assert.Equal(InstallFolderKind.Unrecognized, ctx.State.InstallFolder!.Kind);
    }
}
