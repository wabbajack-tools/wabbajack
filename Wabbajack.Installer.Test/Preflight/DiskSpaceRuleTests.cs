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
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class DiskSpaceRuleTests
{
    private const long GB = 1024L * 1024 * 1024;

    [Theory]
    // Plenty of room everywhere.
    [InlineData("C:\\", 100, "D:\\", 100, 50, 10, DiskSpaceLevel.Ok)]
    // The downloads drive cannot hold what is still to be fetched: hard stop.
    [InlineData("C:\\", 100, "D:\\", 5, 50, 10, DiskSpaceLevel.Block)]
    // The install drive cannot hold a full install: only a warning, updates need far less.
    [InlineData("C:\\", 40, "D:\\", 100, 50, 10, DiskSpaceLevel.Warn)]
    // Same drive: install and remaining downloads add up.
    [InlineData("C:\\", 55, "C:\\", 55, 50, 10, DiskSpaceLevel.Warn)]
    [InlineData("C:\\", 60, "C:\\", 60, 50, 10, DiskSpaceLevel.Ok)]
    // Same drive, remaining bigger than free: the block wins over the warning.
    [InlineData("C:\\", 8, "C:\\", 8, 50, 10, DiskSpaceLevel.Block)]
    // Different drives are judged independently: a full install drive does not block downloads...
    [InlineData("C:\\", 50, "D:\\", 10, 50, 10, DiskSpaceLevel.Ok)]
    [InlineData("C:\\", 10, "D:\\", 10, 50, 10, DiskSpaceLevel.Warn)]
    // ...and the downloads drive being full does not depend on the install drive.
    [InlineData("C:\\", 1000, "D:\\", 9, 50, 10, DiskSpaceLevel.Block)]
    // Nothing left to download never blocks, even with an empty downloads drive.
    [InlineData("C:\\", 100, "D:\\", 0, 50, 0, DiskSpaceLevel.Ok)]
    [InlineData("C:\\", 10, "D:\\", 0, 50, 0, DiskSpaceLevel.Warn)]
    // Drive letters compare case-insensitively.
    [InlineData("c:\\", 55, "C:\\", 55, 50, 10, DiskSpaceLevel.Warn)]
    public void Evaluate(string installRoot, long installFree, string downloadsRoot, long downloadsFree,
        long installBytes, long remaining, DiskSpaceLevel expected)
    {
        var verdict = DiskSpaceRule.Evaluate(new DiskSpaceInput(installRoot, installFree * GB, downloadsRoot,
            downloadsFree * GB, installBytes * GB, remaining * GB));

        Assert.Equal(expected, verdict.Level);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Message));
    }

    [Fact]
    public void MessagesNameTheDriveAndTheSizes()
    {
        var block = DiskSpaceRule.Evaluate(new DiskSpaceInput("C:\\", 100 * GB, "D:\\", 5 * GB, 50 * GB, 10 * GB));
        Assert.Contains("D:\\", block.Message);
        Assert.Contains("10GB", block.Message);
        Assert.Contains("5GB", block.Message);

        var warn = DiskSpaceRule.Evaluate(new DiskSpaceInput("C:\\", 55 * GB, "C:\\", 55 * GB, 50 * GB, 10 * GB));
        Assert.Contains("C:\\", warn.Message);
        Assert.Contains("60GB", warn.Message);
        Assert.Contains("Updating an existing install needs far less", warn.Message);
    }
}

public class DiskSpaceCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly DiskSpaceCheck _check = new();

    public DiskSpaceCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static Directive Directive(long size)
    {
        return new InlineFile {To = "file.txt".ToRelativePath(), Size = size};
    }

    [Fact]
    public async Task PassesWhenThereIsRoom()
    {
        _host.Config.ModList.Directives = new[] {Directive(1), Directive(2)};
        var ctx = _host.Context();
        ctx.State.RemainingDownloadBytes = 1;

        var result = await _check.Run(ctx, new RecordingProgress(), CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("free on", result.Message);
    }

    [Fact]
    public async Task BlocksWhenTheRemainingDownloadsCannotFit()
    {
        var ctx = _host.Context();
        ctx.State.RemainingDownloadBytes = long.MaxValue / 2;

        var result = await _check.Run(ctx, new RecordingProgress(), CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("Not enough space for downloads", result.Message);
    }

    [Fact]
    public async Task FallsBackToDirectiveSizesWhenThereIsNoMetadata()
    {
        _host.Config.Metadata = null;
        _host.Config.ModList.Directives = new[] {Directive(long.MaxValue / 4), Directive(long.MaxValue / 4)};
        var ctx = _host.Context();

        var result = await _check.Run(ctx, new RecordingProgress(), CancellationToken.None);

        Assert.Equal(PreflightState.Warning, result.State);
        Assert.Contains(PreflightAction.ContinueAnyway, result.Actions!);
    }

    [Fact]
    public async Task PrefersTheMetadataInstallSize()
    {
        _host.Config.ModList.Directives = new[] {Directive(long.MaxValue / 4), Directive(long.MaxValue / 4)};
        _host.Config.Metadata = new ModlistMetadata
        {
            DownloadMetadata = new DownloadMetadata {SizeOfInstalledFiles = 1, SizeOfArchives = 1}
        };
        var ctx = _host.Context();

        var result = await _check.Run(ctx, new RecordingProgress(), CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
    }
}
