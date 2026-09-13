using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test;

public class StandardInstallerTest
{
    private readonly TemporaryFileManager _manager;
    private readonly AbsolutePath _modList;
    private readonly IServiceProvider _provider;
    private readonly DTOSerializer _serializer;

    public StandardInstallerTest(IServiceProvider provider, DTOSerializer serializer, TemporaryFileManager manager)
    {
        _provider = provider;
        _serializer = serializer;
        _modList = "TestData/MO2AndSKSETest.wabbajack".ToRelativePath().RelativeTo(KnownFolders.EntryPoint);
        _manager = manager;
    }

    [Fact]
    public async Task CanLoadModlistDefinition()
    {
        var modlist = await StandardInstaller.LoadFromFile(_serializer, _modList);
        Assert.Equal("MO2AndSKSETest", modlist.Name);
    }

    /// <summary>
    ///     The list's two archives are plain Http downloads; preflight fetches them, the installer only
    ///     installs.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public async Task CanInstallAList()
    {
        var modlist = await StandardInstaller.LoadFromFile(_serializer, _modList);
        using var scope = _provider.CreateScope();
        var config = _provider.GetService<InstallerConfiguration>()!;
        await using var installFolder = _manager.CreateFolder();
        config.Install = installFolder;
        config.Downloads = config.Install.Combine("downloads");
        config.ModlistArchive = _modList;
        config.ModList = modlist;
        config.Game = modlist.GameType;
        config.SystemParameters = SystemParameters();

        var configuration = _provider.GetService<Client>();
        configuration.IgnoreMirrorList = true;

        // The stubbed game folder is empty; the game-files check wants the game's required files there.
        var gameFolder = _provider.GetRequiredService<IGameLocator>().GameLocation(modlist.GameType);
        foreach (var required in modlist.GameType.MetaData().RequiredFiles)
        {
            var file = gameFolder.Combine(required);
            if (!file.FileExists())
                await file.WriteAllTextAsync("");
        }

        var runner = PreflightRunner.Create(_provider, config,
            new PreflightOptions {WaitForManualDownloads = false, SendMetrics = false});
        var outcome = await runner.RunAll(CancellationToken.None);
        Assert.True(outcome.Ready, string.Join("; ",
            outcome.Checks.Where(c => !c.IsSatisfied).Select(c => $"{c.Title}: {c.Message} {c.Detail}")));

        var installer = _provider.GetService<StandardInstaller>();
        Assert.True(await installer.Begin(CancellationToken.None) == InstallResult.Succeeded);

        Assert.True("ModOrganizer.exe".ToRelativePath().RelativeTo(installFolder).FileExists());
    }

    /// <summary>
    ///     Without preflight nothing fetches archives: an archive that is not in the downloads folder ends the
    ///     install as DownloadFailed, and no downloader is asked for it. The fake downloaders record every
    ///     attempt, so the archive is registered with them; a real download would leave a trace.
    /// </summary>
    [Fact]
    public async Task InstallerReturnsDownloadFailedWhenAnArchiveIsMissing()
    {
        var bytes = Encoding.UTF8.GetBytes("archive the installer must not fetch " + Guid.NewGuid());
        var state = new Http {Url = new Uri($"https://example.invalid/{Guid.NewGuid()}/missing.zip")};
        var archive = new Archive
        {
            Name = "missing.zip",
            Size = bytes.Length,
            Hash = await bytes.Hash(),
            State = state
        };
        var server = _provider.GetRequiredService<FakeDownloadServer>();
        server.Serve(state, bytes);

        using var scope = _provider.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<InstallerConfiguration>();
        await using var installFolder = _manager.CreateFolder();
        config.Install = installFolder;
        config.Downloads = config.Install.Combine("downloads");
        config.ModlistArchive = _modList;
        config.Game = Game.SkyrimSpecialEdition;
        config.SystemParameters = SystemParameters();
        config.ModList = new ModList
        {
            Name = "Missing archive",
            GameType = Game.SkyrimSpecialEdition,
            Archives = new[] {archive},
            Directives = new Directive[]
            {
                new FromArchive
                {
                    To = "mods/missing/readme.txt".ToRelativePath(),
                    Hash = archive.Hash,
                    Size = archive.Size,
                    ArchiveHashPath = new HashRelativePath(archive.Hash, "readme.txt".ToRelativePath())
                }
            }
        };

        var installer = scope.ServiceProvider.GetRequiredService<StandardInstaller>();
        Assert.Equal(InstallResult.DownloadFailed, await installer.Begin(CancellationToken.None));

        Assert.Equal(0, server.Attempts(state));
        Assert.False(config.Downloads.Combine(archive.Name).FileExists());
    }

    private static SystemParameters SystemParameters()
    {
        return new SystemParameters
        {
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            SystemMemorySize = 8L * 1024 * 1024 * 1024,
            SystemPageSize = 8L * 1024 * 1024 * 1024,
            VideoMemorySize = 8L * 1024 * 1024 * 1024
        };
    }
}
