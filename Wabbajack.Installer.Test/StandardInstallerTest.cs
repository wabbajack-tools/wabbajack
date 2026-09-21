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
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Hashing.PHash;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;
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

    /// <summary>
    ///     A machine with no game folder at all is no longer refused. Preflight lets a run carry on when the
    ///     game is not installed and a source can supply the files the list takes from it, and those land in
    ///     the downloads folder - so the install has to get as far as looking for them.
    ///     <para>
    ///         What that looks like here: the archive is not in downloads either, so the install ends at the
    ///         missing-archive check, naming the file. Before this it ended one step earlier with
    ///         <see cref="InstallResult.GameMissing" />, which is a different thing to go and fix.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task AMissingGameFolderNoLongerEndsTheInstall()
    {
        await using var installFolder = _manager.CreateFolder();
        var config = await MissingArchiveList(installFolder);

        var result = await InstallerWith(new FakeGameLocator(), config).Begin(CancellationToken.None);

        Assert.Equal(InstallResult.DownloadFailed, result);
        Assert.Equal(default, config.GameFolder);
    }

    /// <summary>
    ///     A folder that was named and is not there is still a mistake to correct rather than a game to
    ///     fetch: somebody said where the game is, and they were wrong.
    /// </summary>
    [Fact]
    public async Task AGameFolderThatDoesNotExistIsStillInvalid()
    {
        await using var installFolder = _manager.CreateFolder();
        var config = await MissingArchiveList(installFolder);
        config.GameFolder = installFolder.Path.Combine("no-such-game-folder");

        var result = await InstallerWith(new FakeGameLocator(), config).Begin(CancellationToken.None);

        Assert.Equal(InstallResult.GameInvalid, result);
    }

    /// <summary>
    ///     A one-archive list whose archive is nowhere, which is enough to reach the missing-archive check
    ///     without any payload to extract.
    /// </summary>
    private async Task<InstallerConfiguration> MissingArchiveList(AbsolutePath installFolder)
    {
        var bytes = Encoding.UTF8.GetBytes("an archive nobody has " + Guid.NewGuid());
        var archive = new Archive
        {
            Name = "missing.zip",
            Size = bytes.Length,
            Hash = await bytes.Hash(),
            State = new Http {Url = new Uri($"https://example.invalid/{Guid.NewGuid()}/missing.zip")}
        };

        return new InstallerConfiguration
        {
            Install = installFolder,
            Downloads = installFolder.Combine("downloads"),
            ModlistArchive = _modList,
            Game = Game.SkyrimSpecialEdition,
            SystemParameters = SystemParameters(),
            ModList = new ModList
            {
                Name = "No game folder",
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
            }
        };
    }

    /// <summary>
    ///     The registered locator is the stubbed one, which finds every game in the same folder - so a test
    ///     about not having a game folder has to bring its own.
    /// </summary>
    private StandardInstaller InstallerWith(IGameLocator locator, InstallerConfiguration config)
    {
        return new StandardInstaller(_provider.GetRequiredService<ILogger<StandardInstaller>>(), config, locator,
            _provider.GetRequiredService<FileExtractor.FileExtractor>(),
            _provider.GetRequiredService<DTOSerializer>(),
            _provider.GetRequiredService<Context>(),
            _provider.GetRequiredService<FileHashCache>(),
            _provider.GetRequiredService<DownloadDispatcher>(),
            _provider.GetRequiredService<ParallelOptions>(),
            _provider.GetRequiredService<IResource<IInstaller>>(),
            _provider.GetRequiredService<Client>(),
            _provider.GetRequiredService<IImageLoader>());
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
