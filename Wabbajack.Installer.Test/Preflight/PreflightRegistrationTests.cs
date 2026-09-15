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

        // nexus-login sits after the inventory on purpose: it asks about the files that are still missing,
        // so a user with nothing left to fetch from Nexus is never stopped for a login they do not need.
        Assert.Equal(new[]
        {
            PreflightCheckIds.GameInstalled, PreflightCheckIds.GameFiles, PreflightCheckIds.ArchiveInventory,
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
