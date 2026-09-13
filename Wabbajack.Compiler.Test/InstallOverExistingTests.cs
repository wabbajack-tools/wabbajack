using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Common;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

/// <summary>
///     Installing on top of an install that is already there. The installer no longer downloads anything, so
///     the archives it still needs have to be in the downloads folder before Begin, and the archives it no
///     longer needs must not be demanded. These cover the two cases where the contents of the downloads
///     folder and the modlist's archive list come apart: a re-install of an unchanged list, and an update
///     whose pruned archives are gone from downloads.
///     The fixture the harness builds on comes from authored-files.wabbajack.org, so the whole class needs
///     network.
/// </summary>
[Trait("Category", "RequiresNetwork")]
public class InstallOverExistingTests : IAsyncLifetime
{
    private const string ByHandArchive = "by-hand.zip";

    private readonly ModListHarness _harness;
    private readonly TemporaryFileManager _manager;
    private readonly IServiceScope _scope;
    private Mod _mod;

    public InstallOverExistingTests(IServiceProvider serviceProvider, TemporaryFileManager manager)
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

    [Fact]
    public async Task ReinstallOverAnIdenticalInstallSucceeds()
    {
        Assert.NotNull(await _harness.Compile());

        Assert.Equal(InstallResult.Succeeded, await _harness.InstallWithResult());
        foreach (var file in _mod.FullPath.EnumerateFiles())
            _harness.VerifyInstalledFile(file);

        // Every file the list installs is now in place with the hash the list expects, so OptimizeModlist
        // prunes the whole thing and ModList.Archives comes out empty. The run must still end Succeeded
        // rather than tripping the installer's missing-archive check.
        Assert.Equal(InstallResult.Succeeded, await _harness.InstallWithResult());
        foreach (var file in _mod.FullPath.EnumerateFiles())
            _harness.VerifyInstalledFile(file);
    }

    [Fact]
    public async Task UpdateSucceedsWhenAPrunedArchiveIsMissingFromDownloads()
    {
        await AddByHandMod();

        var modlist = await _harness.Compile();
        Assert.NotNull(modlist);
        Assert.Equal(2, modlist!.Archives.Length);
        Assert.Contains(modlist.Archives, a => a.State is Manual);

        Assert.Equal(InstallResult.Succeeded, await _harness.InstallWithResult());

        // Leave the install in place, but take one of the first mod's files back out so the update still
        // has something to install and still needs that mod's archive.
        var reinstalled = _mod.FullPath.EnumerateFiles().MinBy(f => f.Size());
        _harness.InstalledPath(reinstalled).Delete();

        var updated = await _harness.Compile();
        Assert.NotNull(updated);
        // The compiled list still carries both archives - it is the install's own pruning that has to drop
        // the one it no longer reads, not the compiler.
        Assert.Equal(2, updated!.Archives.Length);

        // The by-hand mod is untouched on disk, so OptimizeModlist prunes every directive that reads its
        // archive - and must drop the archive with them. PrePlaceDownloads puts it back, so delete it
        // again and install against the downloads folder as it stands.
        await _harness.PrePlaceDownloads();
        _harness.InstallDownloadPath(ByHandArchive).Delete();

        var result = await _harness.InstallWithResult(false);

        Assert.Equal(InstallResult.Succeeded, result);
        Assert.True(_harness.InstalledPath(reinstalled).FileExists());
        foreach (var file in _mod.FullPath.EnumerateFiles())
            _harness.VerifyInstalledFile(file);
    }

    /// <summary>
    ///     A second mod out of a second archive, built here and recorded as a manual download so the two
    ///     archives are genuinely distinct (the same fixture twice would collapse to one archive by hash).
    /// </summary>
    private async Task AddByHandMod()
    {
        await using var zipFolder = _manager.CreateFolder();
        var zipPath = zipFolder.Path.Combine(ByHandArchive);
        using (var zip = ZipFile.Open(zipPath.ToString(), ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("textures/by-hand.txt");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("fetched by hand " + Guid.NewGuid());
        }

        await _harness.AddManualDownload(zipPath);
        var mod = _harness.AddMod("by-hand");
        mod.EnabledIn.Add(_mod.EnabledIn.Single());
        await mod.AddFromArchive(_harness.DownloadPath(ByHandArchive));
    }
}
