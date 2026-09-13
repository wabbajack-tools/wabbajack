using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Common;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

/// <summary>
///     Compiles a real list through the harness, then runs the preflight checklist the way the installer
///     hosts will. The fixture the harness builds on comes from authored-files.wabbajack.org, so the whole
///     class needs network.
/// </summary>
[Trait("Category", "RequiresNetwork")]
public class PreflightEndToEndTests : IAsyncLifetime
{
    private readonly ModListHarness _harness;
    private readonly IServiceScope _scope;
    private readonly TemporaryFileManager _manager;
    private Mod _mod;

    public PreflightEndToEndTests(IServiceProvider serviceProvider, TemporaryFileManager manager)
    {
        _scope = serviceProvider.CreateScope();
        _harness = _scope.ServiceProvider.GetService<ModListHarness>()!;
        _manager = manager;
    }

    public async Task InitializeAsync()
    {
        _mod = await _harness.InstallMod(Ext.Zip,
            new Uri(
                "https://authored-files.wabbajack.org/Tonal%20Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794f"));
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    private static CheckStatus Check(PreflightOutcome outcome, string id)
    {
        return outcome.Checks.Single(c => c.Id == id);
    }

    [Fact]
    public async Task PreflightPassesWhenDownloadsArePrePlaced()
    {
        var modlist = await _harness.Compile();
        Assert.NotNull(modlist);
        await _harness.PrePlaceDownloads();

        var outcome = await _harness.Preflight();

        Assert.True(outcome.Ready, string.Join(Environment.NewLine, outcome.Checks.Select(c => $"{c.Id}: {c.State} {c.Message}")));
        Assert.All(outcome.Checks, c => Assert.Equal(PreflightState.Passed, c.State));
        Assert.Equal("Nothing to download", Check(outcome, PreflightCheckIds.AutomatedDownloads).Message);
        Assert.Equal("Nothing to download by hand", Check(outcome, PreflightCheckIds.ManualDownloads).Message);
    }

    [Fact]
    public async Task PreflightListsMissingManualArchiveWithUrl()
    {
        // A second archive the list can only get by hand: a zip built here, recorded with a manual URL.
        await using var zipFolder = _manager.CreateFolder();
        var zipPath = zipFolder.Path.Combine("by-hand.zip");
        using (var zip = ZipFile.Open(zipPath.ToString(), ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("textures/by-hand.txt");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("fetched by hand " + Guid.NewGuid());
        }

        var url = await _harness.AddManualDownload(zipPath);
        var manualMod = _harness.AddMod("by-hand");
        manualMod.EnabledIn.Add(_mod.EnabledIn.Single());
        await manualMod.AddFromArchive(_harness.DownloadPath("by-hand.zip"));

        var modlist = await _harness.Compile();
        Assert.NotNull(modlist);
        Assert.Contains(modlist!.Archives, a => a.State is Manual);

        await _harness.PrePlaceDownloads();
        _harness.InstallDownloadPath("by-hand.zip").Delete();

        var outcome = await _harness.Preflight();

        Assert.False(outcome.Ready);
        var manual = Check(outcome, PreflightCheckIds.ManualDownloads);
        Assert.Equal(PreflightState.NeedsUser, manual.State);
        Assert.StartsWith("1 files must be downloaded by hand", manual.Message);
        Assert.Contains("by-hand.zip", manual.Detail);
        Assert.Contains(url.ToString(), manual.Detail);
        Assert.Contains(PreflightAction.Rescan, manual.Actions);
        Assert.StartsWith("0 downloaded, 1 moved to manual", Check(outcome, PreflightCheckIds.AutomatedDownloads).Message);
    }

    [Fact]
    public async Task PreflightThenInstallSucceeds()
    {
        var modlist = await _harness.Compile();
        Assert.NotNull(modlist);
        await _harness.PrePlaceDownloads();

        var outcome = await _harness.Preflight();
        Assert.True(outcome.Ready);

        Assert.True(await _harness.Install());
        foreach (var file in _mod.FullPath.EnumerateFiles())
            _harness.VerifyInstalledFile(file);
    }
}
