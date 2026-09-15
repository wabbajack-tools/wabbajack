#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     The repair against a stand-in for the depot. Everything here is about what the rule decides -
///     which version to ask for, what to do with the answer, and what gets written where - so the fake
///     answers "this file at this version" from a table and nothing touches Steam or the network.
/// </summary>
public class GameFileRepairTests : IDisposable
{
    private const string Version = "1.6.640";

    private readonly GameFilesCheck _check = new();
    private readonly PreflightTestHost _host;
    private readonly RecordingProgress _progress = new();
    private readonly FakeGameFileRestorer _restorer = new();

    public GameFileRepairTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
        _host.Restorer = _restorer;
        _host.Locator.SteamBuildIds[Game.SkyrimSpecialEdition] = "1234567";
        _restorer.KnownVersions.Add(Version);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static async Task<Archive> GameFile(string relative, string content, string version = Version)
    {
        var archive = await PreflightTestHost.ArchiveFor(relative.Replace('/', '_'), content);
        archive.State = new GameFileSource
        {
            Game = Game.SkyrimSpecialEdition,
            GameFile = relative.ToRelativePath(),
            Hash = archive.Hash,
            GameVersion = version
        };
        return archive;
    }

    /// <summary>Runs game-files over the configured modlist and hands back what it found to repair.</summary>
    private async Task<(PreflightContext Ctx, IReadOnlyList<RepairableGameFile> Repairable)> Detect()
    {
        var ctx = _host.Context();
        ctx.State.GameFolder = _host.GameFolder;
        await _host.Inventory(ctx);
        var result = await _check.Run(ctx, _progress, CancellationToken.None);
        Assert.Equal(PreflightState.Failed, result.State);
        return (ctx, ctx.State.RepairableGameFiles);
    }

    private static string Depot(string relative)
    {
        return relative.ToRelativePath().ToString();
    }

    [Fact]
    public async Task AMissingFileIsFetchedAndPlacedInTheDownloadsFolder()
    {
        var archive = await GameFile("Data/Dawnguard.esm", "dlc bytes");
        _host.Config.ModList.Archives = new[] {archive};
        _restorer.Files[(null, Depot("Data/Dawnguard.esm"))] = "dlc bytes";

        var (ctx, repairable) = await Detect();
        var results = await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(GameFileRepairStatus.Repaired, result.Status);

        var placed = _host.Config.Downloads.Combine(archive.Name);
        Assert.True(placed.FileExists());
        Assert.Equal(placed, result.Placed);
        Assert.Equal(archive.Hash, await _host.Cache.FileHashCachedAsync(placed, CancellationToken.None));

        // A .meta beside it, the way every other placement in preflight leaves one.
        var meta = await placed.WithExtension(Ext.Meta).ReadAllTextAsync();
        Assert.Contains("[General]", meta);
        Assert.Contains("gameName=SkyrimSpecialEdition", meta);

        // And the blackboard records it, so a re-run of the check sees it without another folder walk.
        Assert.Equal(placed, ctx.State.HashedArchives[archive.Name]);
    }

    /// <summary>
    ///     The whole point of putting it in the downloads folder: the installer takes game files by hash out
    ///     of one map of the downloads folder and the game folders. So the check that asked for the repair
    ///     passes afterwards, and the installer's own inventory - the same <c>ArchiveInventory.Scan</c> that
    ///     <c>AInstaller.HashArchives</c> calls - finds it, with nothing written to the game folder.
    /// </summary>
    [Fact]
    public async Task ARepairedFileSatisfiesTheCheckAndTheInstallersOwnInventory()
    {
        var archive = await GameFile("Data/Dawnguard.esm", "dlc bytes");
        _host.Config.ModList.Archives = new[] {archive};
        _restorer.Files[(null, Depot("Data/Dawnguard.esm"))] = "dlc bytes";

        var (ctx, repairable) = await Detect();
        await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None);

        var after = await _check.Run(ctx, new RecordingProgress(), CancellationToken.None);
        Assert.Equal(PreflightState.Passed, after.State);
        Assert.Empty(ctx.State.RepairableGameFiles);

        var byHash = await ArchiveInventory.Scan(new[] {archive}, ctx.Config.Downloads,
            new[] {_host.GameFolder}, _host.Cache, _host.Limiter, NullLogger.Instance, CancellationToken.None);
        Assert.True(byHash.ContainsKey(archive.Hash));
        Assert.Equal(ctx.Config.Downloads, byHash[archive.Hash].Parent);

        Assert.False(_host.GameFolder.Combine("Data/Dawnguard.esm").FileExists());
    }

    /// <summary>
    ///     A file that is there and wrong can only mean the game has moved past the build the list wants, so
    ///     the version the list recorded is the first thing asked for - which is the one question the version
    ///     index exists to answer. Today's build is not even offered first.
    /// </summary>
    [Fact]
    public async Task AMismatchedFileIsResolvedAtTheVersionTheListRecorded()
    {
        var archive = await GameFile("Data/Skyrim.esm", "1.6.640 bytes");
        await PreflightTestHost.WriteFile(_host.GameFolder.Combine("Data/Skyrim.esm"), "some newer build");
        _host.Config.ModList.Archives = new[] {archive};

        // The current build has the file too, and it is the wrong one. Only the indexed version is right.
        _restorer.Files[(null, Depot("Data/Skyrim.esm"))] = "some newer build";
        _restorer.Files[(Version, Depot("Data/Skyrim.esm"))] = "1.6.640 bytes";

        var (ctx, repairable) = await Detect();
        Assert.Equal(GameFileProblem.Mismatched, Assert.Single(repairable).Problem);

        var result = Assert.Single(await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None));

        Assert.Equal(GameFileRepairStatus.Repaired, result.Status);
        Assert.Equal(Version, result.Version);
        Assert.Equal(Version, Assert.Single(_restorer.Asked).Version);
    }

    /// <summary>
    ///     A file the user never installed is usually just the one the game ships today, and that needs no
    ///     index lookup at all - so it is asked for first, and the recorded version is only the fallback.
    /// </summary>
    [Fact]
    public async Task AMissingFileAsksForTheCurrentBuildFirstAndFallsBackToTheRecordedVersion()
    {
        var archive = await GameFile("Data/Dawnguard.esm", "the old dlc");
        _host.Config.ModList.Archives = new[] {archive};

        // The current build has moved on, so the first question is answered and answered wrongly.
        _restorer.Files[(null, Depot("Data/Dawnguard.esm"))] = "a newer dlc";
        _restorer.Files[(Version, Depot("Data/Dawnguard.esm"))] = "the old dlc";

        var (ctx, repairable) = await Detect();
        var result = Assert.Single(await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None));

        Assert.Equal(GameFileRepairStatus.Repaired, result.Status);
        Assert.Equal(new string?[] {null, Version}, _restorer.Asked.Select(a => a.Version).ToArray());
        Assert.Equal(archive.Hash,
            await _host.Cache.FileHashCachedAsync(_host.Config.Downloads.Combine(archive.Name),
                CancellationToken.None));
    }

    /// <summary>
    ///     The index carries the versions lists have actually been built against, and a list can name one
    ///     nobody has recorded. Said plainly, and not as a fetch that mysteriously produced nothing.
    /// </summary>
    [Fact]
    public async Task AVersionTheIndexDoesNotCarryIsSaidPlainly()
    {
        var archive = await GameFile("Data/Skyrim.esm", "1.5.97 bytes", "1.5.97.0");
        await PreflightTestHost.WriteFile(_host.GameFolder.Combine("Data/Skyrim.esm"), "some newer build");
        _host.Config.ModList.Archives = new[] {archive};

        var (ctx, repairable) = await Detect();
        var result = Assert.Single(await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None));

        Assert.Equal(GameFileRepairStatus.Failed, result.Status);
        Assert.Contains("no record of", result.Message);
        Assert.Contains("1.5.97.0", result.Message);
        Assert.False(_host.Config.Downloads.Combine(archive.Name).FileExists());
    }

    [Fact]
    public async Task AFileNoManifestCarriesIsReportedAsNotFound()
    {
        var archive = await GameFile("Data/CreationKit.exe", "ck bytes");
        _host.Config.ModList.Archives = new[] {archive};

        var (ctx, repairable) = await Detect();
        var result = Assert.Single(await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None));

        Assert.Equal(GameFileRepairStatus.Failed, result.Status);
        Assert.Contains("Data\\CreationKit.exe", result.Message);
        Assert.False(_host.Config.Downloads.Combine(archive.Name).FileExists());
    }

    /// <summary>
    ///     The depot hash proves Steam handed over what it meant to. Only the modlist's own xxHash64 proves
    ///     it is the file this install needs, and a file that fails it must not be left in the downloads
    ///     folder: it would satisfy nothing, and would read later as a corrupt download of something else.
    /// </summary>
    [Fact]
    public async Task BytesThatDoNotMatchTheModlistHashAreRejectedAndNotPlaced()
    {
        var archive = await GameFile("Data/Dawnguard.esm", "the bytes the list wants");
        _host.Config.ModList.Archives = new[] {archive};
        _restorer.Files[(null, Depot("Data/Dawnguard.esm"))] = "some entirely different bytes";
        _restorer.Files[(Version, Depot("Data/Dawnguard.esm"))] = "some entirely different bytes";

        var (ctx, repairable) = await Detect();
        var result = Assert.Single(await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None));

        Assert.Equal(GameFileRepairStatus.WrongContent, result.Status);
        Assert.Contains("has not been kept", result.Message);

        var placed = _host.Config.Downloads.Combine(archive.Name);
        Assert.False(placed.FileExists());
        Assert.False(placed.WithExtension(Ext.Meta).FileExists());
        Assert.Empty(ctx.Config.Downloads.EnumerateFiles(Ext.WjIncoming, false));
        Assert.False(ctx.State.HashedArchives.ContainsKey(archive.Name));

        // And the check still says the same thing it did before.
        Assert.Equal(PreflightState.Failed,
            (await _check.Run(ctx, new RecordingProgress(), CancellationToken.None)).State);
    }

    /// <summary>Nothing is fetched, and nobody is asked twice, when there is no login to fetch with.</summary>
    [Fact]
    public async Task NothingIsAttemptedWithoutALogin()
    {
        _restorer.Ready = false;
        _restorer.NotReadyReason = "Log into Steam first.";
        _host.Config.ModList.Archives = new[] {await GameFile("Data/Dawnguard.esm", "dlc bytes")};

        var (ctx, repairable) = await Detect();
        var result = Assert.Single(await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None));

        Assert.Equal(GameFileRepairStatus.NotAttempted, result.Status);
        Assert.Equal("Log into Steam first.", result.Message);
        Assert.Empty(_restorer.Asked);
    }

    /// <summary>A fetch that falls over is reported against the file, not thrown at the caller.</summary>
    [Fact]
    public async Task AFetchThatThrowsIsReportedAgainstTheFile()
    {
        _restorer.Throws = () => new InvalidOperationException("the depot fell over");
        _host.Config.ModList.Archives = new[] {await GameFile("Data/Dawnguard.esm", "dlc bytes")};

        var (ctx, repairable) = await Detect();
        var result = Assert.Single(await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None));

        Assert.Equal(GameFileRepairStatus.Failed, result.Status);
        Assert.Contains("the depot fell over", result.Message);
    }

    /// <summary>
    ///     One list can span versions - each GameFileSource carries its own - so the work is ordered by game
    ///     and then version, and each file is asked for at the version it was recorded at.
    /// </summary>
    [Fact]
    public async Task FilesAreGroupedByGameAndVersion()
    {
        _restorer.KnownVersions.Add("1.5.97.0");

        var newer = await GameFile("Data/Skyrim.esm", "newer esm");
        var older = await GameFile("Data/Update.esm", "older update", "1.5.97.0");
        await PreflightTestHost.WriteFile(_host.GameFolder.Combine("Data/Skyrim.esm"), "something else again");
        await PreflightTestHost.WriteFile(_host.GameFolder.Combine("Data/Update.esm"), "and something else");
        _host.Config.ModList.Archives = new[] {newer, older};

        _restorer.Files[("1.6.640", Depot("Data/Skyrim.esm"))] = "newer esm";
        _restorer.Files[("1.5.97.0", Depot("Data/Update.esm"))] = "older update";

        var (ctx, repairable) = await Detect();
        var results = await GameFileRepair.Run(ctx, repairable, _progress, CancellationToken.None);

        Assert.All(results, r => Assert.Equal(GameFileRepairStatus.Repaired, r.Status));
        Assert.Equal(new[] {"1.5.97.0", "1.6.640"}, results.Select(r => r.Version).ToArray());
        Assert.Equal(new[] {"1.5.97.0", "1.6.640"}, _restorer.Asked.Select(a => a.Version).ToArray());
    }
}
