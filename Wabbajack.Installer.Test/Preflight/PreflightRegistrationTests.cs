#nullable enable
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Installer.Preflight;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     The test host uses AddOSIntegrated, which calls AddPreflight; this pins down what that registers.
/// </summary>
public class PreflightRegistrationTests : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly PreflightTestHost _host;

    public PreflightRegistrationTests(IServiceProvider provider)
    {
        _provider = provider;
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public void RegistersEveryCheckInOrder()
    {
        var ids = _provider.GetServices<IPreflightCheck>().OrderBy(c => c.Order).Select(c => c.Id).ToArray();

        // Both game-files and nexus-login sit after the inventory on purpose, for the same reason: each
        // asks about what this install still has left to do, so neither stops a user over something nobody
        // was going to touch. game-files also reads the inventory's hash map, which is how a repaired file
        // sitting in the downloads folder counts as present.
        Assert.Equal(new[]
        {
            PreflightCheckIds.GameInstalled, PreflightCheckIds.ArchiveInventory, PreflightCheckIds.GameFiles,
            PreflightCheckIds.UnsupportedArchives, PreflightCheckIds.NexusLogin,
            PreflightCheckIds.ManualDownloads, PreflightCheckIds.AutomatedDownloads, PreflightCheckIds.DiskSpace
        }, ids);
    }

    [Fact]
    public void AcquirerIsTransient()
    {
        var first = _provider.GetRequiredService<IManualDownloadAcquirer>();
        var second = _provider.GetRequiredService<IManualDownloadAcquirer>();

        Assert.IsType<ManualDownloadAcquirer>(first);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void RunnerCanBeCreatedFromTheContainer()
    {
        var runner = PreflightRunner.Create(_provider, _host.Config, new PreflightOptions {SendMetrics = false});

        Assert.Equal(8, runner.Checks.Count);
        Assert.NotNull(runner.Context.Acquirer);
        Assert.NotNull(runner.Context.DownloadLimiter);
    }
}
