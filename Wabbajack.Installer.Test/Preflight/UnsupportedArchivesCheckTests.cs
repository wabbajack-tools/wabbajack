#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class UnsupportedArchivesCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly UnsupportedArchivesCheck _check = new();
    private readonly RecordingProgress _progress = new();

    public UnsupportedArchivesCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private async Task<PreflightContext> ContextWithMissing(params Archive[] missing)
    {
        var ctx = _host.Context();
        ctx.State.Missing = missing.ToList();
        ctx.State.RemainingDownloadBytes = missing.Sum(a => a.Size);
        return ctx;
    }

    private static Task<Archive> Missing(string name, IDownloadState state)
    {
        return PreflightTestHost.ArchiveFor(name, "bytes for " + name, state);
    }

    [Fact]
    public async Task SupportedMissingArchivesPass()
    {
        var http = await Missing("http.7z", new Http {Url = new Uri("https://example.invalid/http.7z")});
        var nexus = await Missing("nexus.7z", new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 2});
        var ctx = await ContextWithMissing(http, nexus);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(2, ctx.State.Missing.Count);
        Assert.Empty(_progress.Archives);
    }

    [Fact]
    public async Task CreationClubContentFailsAndLeavesTheQueue()
    {
        var cc = await Missing("cc.ba2", new Bethesda {Game = Game.SkyrimSpecialEdition, IsCCMod = true, ContentId = "x"});
        var http = await Missing("http.7z", new Http {Url = new Uri("https://example.invalid/http.7z")});
        var ctx = await ContextWithMissing(cc, http);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("1 Creation Club items must be installed through the game before installing this list",
            result.Message);
        Assert.Equal(new[] {"http.7z"}, ctx.State.Missing.Select(a => a.Name));
        Assert.Equal(http.Size, ctx.State.RemainingDownloadBytes);
        Assert.Equal(ArchiveState.Unsupported, _progress.LastStates()["cc.ba2"]);
        Assert.Contains("cc.ba2", result.Detail);
    }

    [Fact]
    public async Task LegacySourcesFailAsUnsupported()
    {
        var ll = await Missing("ll.7z", new DeprecatedLoversLab());
        var tes = await Missing("tes.7z", new TESAlliance());
        var ctx = await ContextWithMissing(ll, tes);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("2 archives come from an unsupported source", result.Message);
        Assert.DoesNotContain("Creation Club", result.Message);
        Assert.Empty(ctx.State.Missing);
        Assert.Equal(0, ctx.State.RemainingDownloadBytes);
    }

    [Fact]
    public async Task MixedProblemsAreBothReported()
    {
        var cc = await Missing("cc.ba2", new Bethesda {Game = Game.SkyrimSpecialEdition, IsCCMod = true, ContentId = "x"});
        var ll = await Missing("ll.7z", new DeprecatedLoversLab());
        var ctx = await ContextWithMissing(cc, ll);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("1 Creation Club items", result.Message);
        Assert.Contains("1 archives come from an unsupported source", result.Message);
    }

    [Fact]
    public async Task NothingMissingPasses()
    {
        var ctx = await ContextWithMissing();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);
        Assert.Equal(PreflightState.Passed, result.State);
    }
}
