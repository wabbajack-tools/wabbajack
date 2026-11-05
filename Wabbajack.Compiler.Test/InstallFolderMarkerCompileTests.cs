using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class InstallFolderMarkerCompileTests : IAsyncLifetime
{
    private readonly ModListHarness _harness;
    private readonly IServiceScope _scope;

    public InstallFolderMarkerCompileTests(IServiceProvider serviceProvider)
    {
        _scope = serviceProvider.CreateScope();
        _harness = _scope.ServiceProvider.GetService<ModListHarness>()!;
    }

    public async Task InitializeAsync()
    {
        await _harness.InstallGeneratedMod();
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TheMarkerAnInstallLeavesBehindIsNotCompiledIntoTheList()
    {
        await InstallFolderMarker.Write(_harness._source, "Some List", "repo/some-list", "1.0.0.0");
        Assert.True(InstallFolderMarker.PathIn(_harness._source).FileExists());

        var modlist = await _harness.Compile();

        Assert.NotNull(modlist);
        Assert.DoesNotContain(modlist!.Directives,
            d => d.To.FileName == InstallFolderMarker.FileName);
    }

    [Fact]
    public async Task ARecompiledListStillInstallsAndGetsItsOwnMarker()
    {
        await InstallFolderMarker.Write(_harness._source, "Some List", null, "1.0.0.0");

        Assert.NotNull(await _harness.Compile());
        Assert.True(await _harness.Install());
    }

    [Fact]
    public async Task AnInstallThatFailsPartWayStillLeavesTheMarker()
    {
        Assert.NotNull(await _harness.Compile());

        var result = await _harness.InstallWithResult(prePlaceDownloads: false);

        Assert.Equal(InstallResult.DownloadFailed, result);
        var marker = InstallFolderMarker.Read(_harness.InstalledPath(_harness._source.Combine("x")).Parent);
        Assert.NotNull(marker);
        Assert.False(string.IsNullOrEmpty(marker!.ModListName));
    }
}
