using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Networking.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test;

/// <summary>
///     The whole resolution chain against the real thing: Wabbajack's version index, Steam's depots, and a
///     file that actually comes down. Everything below the seam is faked in
///     <c>SteamGameFileRestorerTests</c>; what only a live run can show is that the index still answers, that
///     the ids it hands back are ones Steam will still serve, and that what arrives is the file a modlist
///     built against that version would have recorded.
///     <para>
///         Needs a stored Steam login (<c>steam-login</c>) and an account that owns Skyrim Special Edition,
///         so it is out of the offline lane on both counts. The file is a 37KB Creation Club plugin, chosen
///         because it is the smallest real game file in the depot - proving the chain does not need to cost
///         a gigabyte.
///     </para>
/// </summary>
[Collection("CLI")]
[Trait("Category", "RequiresOAuth")]
[Trait("Category", "RequiresNetwork")]
public class SteamGameFileRepairLiveTests : IDisposable
{
    private const string Version = "1.6.640.0";
    private const string GameFile = "Data/ccBGSSSE037-Curios.esl";

    /// <summary>What Skyrim Special Edition 1.6.640.0 publishes as that file. Measured, not guessed.</summary>
    private const string ExpectedHash = "it6+eSu4OCw=";

    /// <summary>
    ///     What depot 1946183 of app 1946180 publishes as <c>CreationKit.ini</c> today. Measured. Unlike
    ///     the versioned file above this one is whatever the Kit currently ships, so a Bethesda update
    ///     would change it - the depot's own SHA-1 check still holds, and this assertion is the one to
    ///     re-measure if it ever fails.
    /// </summary>
    private const string CreationKitIniHash = "RjpKxrFxfP8=";

    private readonly IServiceProvider _provider;
    private readonly AbsolutePath _temp;

    public SteamGameFileRepairLiveTests(CLITestFixture fixture)
    {
        _provider = fixture.ServiceProvider;
        _temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "wj-steam-live-" + Guid.NewGuid().ToString("N")[..8]).ToAbsolutePath();
        _temp.CreateDirectory();
    }

    public void Dispose()
    {
        if (!_temp.DirectoryExists()) return;
        try
        {
            _temp.DeleteDirectory();
        }
        catch
        {
            // best effort
        }
    }

    /// <summary>
    ///     The signal game-files uses to decide whether the repair is worth offering at all. A real Steam
    ///     install has an app manifest with a build id in it; anything located another way does not, which
    ///     is what keeps the offer away from a GOG or Epic copy whose files no Steam depot will serve.
    /// </summary>
    [Fact]
    public void ARealSteamInstallReportsItsBuildId()
    {
        var locator = new GameLocator(NullLogger<GameLocator>.Instance);

        Assert.True(locator.TryGetSteamBuildId(Game.SkyrimSpecialEdition, out var buildId),
            "This test needs Skyrim Special Edition installed through Steam");
        Assert.NotEmpty(buildId);
    }

    [Fact]
    public async Task TheIndexStillCarriesTheVersionsItSaysItDoes()
    {
        var index = _provider.GetRequiredService<ISteamManifestIndex>();

        var manifests = await index.Get(Game.SkyrimSpecialEdition, Version, CancellationToken.None);

        Assert.NotEmpty(manifests);
        Assert.All(manifests, m =>
        {
            Assert.NotEqual(0u, m.Depot);
            Assert.NotEqual(0ul, m.Manifest);
        });
    }

    /// <summary>A version nobody has indexed is a 404, and has to read as an answer rather than an error.</summary>
    [Fact]
    public async Task AVersionNobodyHasIndexedComesBackEmpty()
    {
        var index = _provider.GetRequiredService<ISteamManifestIndex>();

        Assert.Empty(await index.Get(Game.SkyrimSpecialEdition, "0.0.0.0", CancellationToken.None));
    }

    [Fact]
    public async Task AGameFileIsFetchedAtTheVersionTheIndexRecords()
    {
        var restorer = _provider.GetRequiredService<IGameFileRestorer>();
        Assert.True(restorer.Status().Ready, "This test needs a stored Steam login; run steam-login first");

        var output = _temp.Combine("fetched.esl");
        var result = await restorer.Restore(Game.SkyrimSpecialEdition, Version, GameFile.ToRelativePath(),
            output, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal(Version, result.Version);
        Assert.True(output.FileExists());
        await using var stream = output.Open(System.IO.FileMode.Open);
        Assert.Equal(Hash.FromBase64(ExpectedHash), await stream.Hash(CancellationToken.None));
    }

    /// <summary>
    ///     The same file asked for without a version, which goes to whatever the app publishes today and
    ///     never touches the index. Today's build is not pinned to anything, so this asserts only that a
    ///     file comes down - the point is that the no-version path resolves depots on its own.
    /// </summary>
    [Fact]
    public async Task AGameFileIsFetchedFromWhateverTheGamePublishesNow()
    {
        var restorer = _provider.GetRequiredService<IGameFileRestorer>();
        Assert.True(restorer.Status().Ready, "This test needs a stored Steam login; run steam-login first");

        var output = _temp.Combine("current.esl");
        var result = await restorer.Restore(Game.SkyrimSpecialEdition, null, GameFile.ToRelativePath(),
            output, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.True(output.FileExists());
        Assert.True(output.Size() > 0);
    }

    /// <summary>
    ///     A Creation Kit file, which is the case the whole companion-app search exists for. It lives in no
    ///     depot of app 489830: the Kit is app 1946180 with depots 1946182 and 1946183, and only its
    ///     <c>installdir</c> puts its files in the game's folder, where a modlist records them as the
    ///     game's own.
    ///     <para>
    ///         This also proves the entitlement answer. The Kit is free but not
    ///         <c>common/FreeToDownload</c>: without a licence naming it Steam refuses the PICS access
    ///         token, so the app cannot even be described. Passing means the free licence was in hand or
    ///         was granted on the way through.
    ///     </para>
    ///     <para>
    ///         <c>CreationKit.ini</c> at 2881 bytes is the smallest file the Kit publishes, so the proof
    ///         costs one manifest search and three kilobytes.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task ACreationKitFileComesFromTheKitsOwnApp()
    {
        var restorer = _provider.GetRequiredService<IGameFileRestorer>();
        Assert.True(restorer.Status().Ready, "This test needs a stored Steam login; run steam-login first");

        var output = _temp.Combine("CreationKit.ini");
        var result = await restorer.Restore(Game.SkyrimSpecialEdition, null, "CreationKit.ini".ToRelativePath(),
            output, CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Contains("app 1946180", result.Detail);
        Assert.True(output.FileExists());

        await using var stream = output.Open(System.IO.FileMode.Open);
        Assert.Equal(Hash.FromBase64(CreationKitIniHash), await stream.Hash(CancellationToken.None));
    }

    /// <summary>
    ///     The whole thing, as a user would meet it: preflight finds a game file this install needs and the
    ///     game folder does not have, the repair fetches it into the downloads folder, and the check that
    ///     failed then passes - with nothing written to the game folder.
    ///     The game folder is a synthetic empty one rather than this machine's real install, so the file is
    ///     reliably missing whatever the tester happens to own.
    /// </summary>
    [Fact]
    public async Task PreflightFindsAMissingGameFileAndTheRepairFetchesIt()
    {
        var hash = Hash.FromBase64(ExpectedHash);
        var archive = new Archive
        {
            Name = "ccBGSSSE037-Curios.esl",
            Hash = hash,
            Size = 37476,
            State = new GameFileSource
            {
                Game = Game.SkyrimSpecialEdition,
                GameFile = GameFile.ToRelativePath(),
                Hash = hash,
                GameVersion = Version
            }
        };

        // A game folder with only the files the registry insists on, so nothing this list wants is in it.
        var gameFolder = _temp.Combine("game");
        gameFolder.CreateDirectory();
        foreach (var required in Game.SkyrimSpecialEdition.MetaData().RequiredFiles)
            await gameFolder.Combine(required).WriteAllTextAsync("");

        var downloads = _temp.Combine("downloads");
        var install = _temp.Combine("install");
        downloads.CreateDirectory();
        install.CreateDirectory();

        var config = new InstallerConfiguration
        {
            Game = Game.SkyrimSpecialEdition,
            GameFolder = gameFolder,
            Downloads = downloads,
            Install = install,
            ModList = new ModList
            {
                Name = "Steam repair live test",
                GameType = Game.SkyrimSpecialEdition,
                Archives = new[] {archive},
                // Something has to want the archive, or the pruning drops it and there is nothing to check.
                Directives = new Directive[]
                {
                    new FromArchive
                    {
                        Hash = hash,
                        Size = archive.Size,
                        To = "Data/ccBGSSSE037-Curios.esl".ToRelativePath(),
                        ArchiveHashPath = new HashRelativePath(hash)
                    }
                }
            }
        };

        var runner = PreflightRunner.Create(_provider, config,
            new PreflightOptions {SendMetrics = false, WaitForManualDownloads = false});

        await runner.RunAll(CancellationToken.None);

        var before = Assert.Single(runner.Checks, c => c.Id == PreflightCheckIds.GameFiles);
        Assert.Equal(PreflightState.Failed, before.State);

        // Whether the row offers the repair is not asserted here: this fixture registers
        // StubbedGameLocator, which reports no Steam build id for anything, so the "is this a Steam copy"
        // signal is deliberately absent. ARealSteamInstallReportsItsBuildId covers that signal, and
        // GameFilesCheckTests covers what the check does with it.
        var repairable = Assert.Single(runner.Context.State.RepairableGameFiles);
        Assert.Equal(GameFileProblem.Missing, repairable.Problem);

        var results = await GameFileRepair.Run(runner.Context, runner.Context.State.RepairableGameFiles,
            new SilentProgress(), CancellationToken.None);

        var repaired = Assert.Single(results);
        Assert.Equal(GameFileRepairStatus.Repaired, repaired.Status);
        Assert.Equal(downloads.Combine(archive.Name), repaired.Placed);
        Assert.True(downloads.Combine(archive.Name).FileExists());
        Assert.True(downloads.Combine(archive.Name).WithExtension(Ext.Meta).FileExists());
        Assert.False(gameFolder.Combine(GameFile).FileExists());

        var after = await runner.RunCheck(PreflightCheckIds.GameFiles, CancellationToken.None);
        Assert.Equal(PreflightState.Passed, after.State);
    }

    private sealed class SilentProgress : IPreflightProgress
    {
        public void Report(long current, long total, string? text = null)
        {
        }

        public void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null)
        {
        }

        public void ManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue)
        {
        }
    }
}
