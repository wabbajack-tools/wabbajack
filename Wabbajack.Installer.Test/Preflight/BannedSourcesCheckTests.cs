#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class BannedSourcesCheckTests : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly PreflightTestHost _host;
    private readonly BannedSourcesCheck _check = new();
    private readonly RecordingProgress _progress = new();

    public BannedSourcesCheckTests(IServiceProvider provider)
    {
        _provider = provider;
        _host = new PreflightTestHost(provider);
        _host.Policy.BanListValue = new ServerBanList {Bans = new[] {"modholdmods.com"}};
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static Task<Archive> ArchiveAt(string name, IDownloadState state)
    {
        return PreflightTestHost.ArchiveFor(name, "bytes for " + name, state);
    }

    private async Task<PreflightContext> ContextWith(params IDownloadState[] states)
    {
        var archives = new Archive[states.Length];
        for (var i = 0; i < states.Length; i++)
            archives[i] = await ArchiveAt($"archive{i}.7z", states[i]);
        _host.Config.ModList.Archives = archives;
        return _host.Context();
    }

    [Fact]
    public async Task AListWithNoBannedFilesPasses()
    {
        var ctx = await ContextWith(new Http {Url = new Uri("https://github.com/a/b.7z")},
            new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 2});

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Empty(_progress.Archives);
    }

    [Fact]
    public async Task AManualFileFromABannedHostFailsTheList()
    {
        var ctx = await ContextWith(new Http {Url = new Uri("https://github.com/a/b.7z")},
            new Manual {Url = new Uri("https://www.modholdmods.com/mod/123"), Prompt = "Click download"});

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("1 file from a banned source", result.Message);
        Assert.Contains("archive1.7z: modholdmods.com", result.Detail);
        Assert.DoesNotContain("archive0.7z", result.Detail);
        Assert.Equal(ArchiveState.Unsupported, _progress.LastStates()["archive1.7z"]);
    }

    /// <summary>A ban is about the list, so a copy already in the downloads folder does not get it past.</summary>
    [Fact]
    public async Task FilesAlreadyDownloadedAreStillRefused()
    {
        var ctx = await ContextWith(new Http {Url = new Uri("https://modholdmods.com/a.7z")});
        ctx.State.Missing = new();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
    }

    [Fact]
    public async Task AnEmptyBanListBansNothing()
    {
        _host.Policy.BanListValue = new ServerBanList();
        var ctx = await ContextWith(new Http {Url = new Uri("https://modholdmods.com/a.7z")});

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
    }

    [Fact]
    public async Task ABanListThatWillNotLoadLetsTheInstallGoAhead()
    {
        _host.Policy.ThrowFromBans = new HttpRequestException("offline");
        var ctx = await ContextWith(new Http {Url = new Uri("https://modholdmods.com/a.7z")});

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("offline", result.Detail);
    }

    [Fact]
    public async Task CancellationIsNotMistakenForAnUnreachableList()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _host.Policy.ThrowFromBans = new OperationCanceledException(cts.Token);
        var ctx = await ContextWith();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _check.Run(ctx, _progress, cts.Token));
    }

    /// <summary>
    ///     Through the real runner: a banned list stops at the first row, before the game folder is looked at or
    ///     the downloads folder hashed, so the user is not shown a checklist for an install they will not get.
    /// </summary>
    [Fact]
    public async Task ABannedListStopsTheRunBeforeAnythingElseRuns()
    {
        var ctx = await ContextWith(new Http {Url = new Uri("https://modholdmods.com/a.7z")});
        var runner = new PreflightRunner(_provider.GetServices<IPreflightCheck>(), ctx);

        var outcome = await runner.RunAll(CancellationToken.None);

        Assert.False(outcome.Ready);
        Assert.Equal(PreflightState.Failed, outcome.Checks.Single(c => c.Id == PreflightCheckIds.BannedSources).State);
        Assert.All(outcome.Checks.Where(c => c.Id != PreflightCheckIds.BannedSources),
            c => Assert.Equal(PreflightState.Pending, c.State));
    }
}
