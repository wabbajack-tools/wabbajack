#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public class GameFilesCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly GameFilesCheck _check = new();
    private readonly RecordingProgress _progress = new();

    public GameFilesCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    /// <summary>
    ///     The state game-installed and archive-inventory leave behind. game-files runs after both now, and
    ///     reads the inventory's answer rather than hashing the game folder for itself.
    /// </summary>
    private async Task<PreflightContext> Context()
    {
        var ctx = _host.Context();
        ctx.State.GameFolder = _host.GameFolder;
        await _host.Inventory(ctx);
        return ctx;
    }

    private static async Task<Archive> GameFile(AbsolutePath? writeTo, string relative, string content,
        Game game = Game.SkyrimSpecialEdition, string version = "1.6.640")
    {
        if (writeTo != null)
            await PreflightTestHost.WriteFile(writeTo.Value.Combine(relative), content);
        var archive = await PreflightTestHost.ArchiveFor(relative.Replace('/', '_'), content);
        archive.State = new GameFileSource
        {
            Game = game,
            GameFile = relative.ToRelativePath(),
            Hash = archive.Hash,
            GameVersion = version
        };
        return archive;
    }

    [Fact]
    public async Task PassesWhenEveryGameFileMatches()
    {
        _host.Config.ModList.Archives = new[]
        {
            await GameFile(_host.GameFolder, "Data/Skyrim.esm", "esm bytes"),
            await GameFile(_host.GameFolder, "Data/Skyrim - Voices.bsa", "voices")
        };
        var ctx = await Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("2 game files verified", result.Message);
        Assert.All(_progress.LastStates().Values, s => Assert.Equal(ArchiveState.Present, s));
        Assert.Contains(_progress.Reports, r => r.Current == 2 && r.Total == 2);
        Assert.Empty(ctx.State.RepairableGameFiles);
    }

    [Fact]
    public async Task PassesWhenTheListTakesNothingFromTheGame()
    {
        _host.Config.ModList.Archives = new[] {await PreflightTestHost.ArchiveFor("mod.7z", "not a game file")};
        var result = await _check.Run(await Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Empty(_progress.Archives);
    }

    /// <summary>
    ///     The point of resolving through the inventory: the installer takes game files by hash out of one
    ///     map of the downloads folder and the game folders, so a correct copy under downloads satisfies it
    ///     whatever the game folder looks like. This used to fail the run, which would have made the Steam
    ///     repair - whose whole design is to write to downloads and never to the game folder - invisible to
    ///     the check that asked for it.
    /// </summary>
    [Fact]
    public async Task AGameFileInTheDownloadsFolderCountsAsPresent()
    {
        var archive = await GameFile(null, "Data/Dawnguard.esm", "dlc bytes");
        await PreflightTestHost.WriteFile(_host.Config.Downloads.Combine(archive.Name), "dlc bytes");
        _host.Config.ModList.Archives = new[] {archive};

        var result = await _check.Run(await Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Equal(ArchiveState.Present, _progress.LastStates()["Data_Dawnguard.esm"]);
    }

    /// <summary>
    ///     And the other half of running after the inventory: an archive the pruning dropped is not asked
    ///     about at all, so a game file an update was never going to read cannot stop the run.
    /// </summary>
    [Fact]
    public async Task AGameFileThisInstallDoesNotNeedIsNotChecked()
    {
        var needed = await GameFile(_host.GameFolder, "Data/Skyrim.esm", "esm bytes");
        var notNeeded = await GameFile(null, "Data/Dawnguard.esm", "dlc bytes");
        _host.Config.ModList.Archives = new[] {needed, notNeeded};

        var ctx = await Context();
        // What RequiredArchives.Compute would have returned for an install that already has everything the
        // second archive feeds.
        ctx.State.RequiredArchives = new[] {needed};

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.DoesNotContain("Data_Dawnguard.esm", _progress.LastStates().Keys);
    }

    [Fact]
    public async Task AMissingGameFileFails()
    {
        _host.Config.ModList.Archives = new[]
        {
            await GameFile(_host.GameFolder, "Data/Skyrim.esm", "esm bytes"),
            await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")
        };

        var ctx = await Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("1 game file is missing", result.Message);
        Assert.Contains("Data_Dawnguard.esm", result.Message);
        Assert.Equal(ArchiveState.Missing, _progress.LastStates()["Data_Dawnguard.esm"]);
        Assert.Equal(ArchiveState.Present, _progress.LastStates()["Data_Skyrim.esm"]);
        Assert.Null(result.Actions);

        var repairable = Assert.Single(ctx.State.RepairableGameFiles);
        Assert.Equal(GameFileProblem.Missing, repairable.Problem);
        Assert.Equal("Data_Dawnguard.esm", repairable.Archive.Name);
    }

    [Fact]
    public async Task AHashMismatchReportsTheVersionTheListWasBuiltAgainst()
    {
        var archive = await GameFile(null, "Data/Skyrim.esm", "expected bytes");
        await PreflightTestHost.WriteFile(_host.GameFolder.Combine("Data/Skyrim.esm"), "other bytes");
        _host.Config.ModList.Archives = new[] {archive};

        var ctx = await Context();
        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("1 game file doesn't match", result.Message);
        Assert.Contains("built against 1.6.640; you have an unknown version", result.Message);
        Assert.Equal(ArchiveState.Failed, _progress.LastStates()["Data_Skyrim.esm"]);
        Assert.Contains("Mismatched:", result.Detail);

        var repairable = Assert.Single(ctx.State.RepairableGameFiles);
        Assert.Equal(GameFileProblem.Mismatched, repairable.Problem);
        Assert.Equal("1.6.640", repairable.Version);
    }

    [Fact]
    public async Task MissingRequiredFilesFailBeforeAnyHashing()
    {
        _host.GameFolder.Combine("SkyrimSE.exe").Delete();
        _host.Config.ModList.Archives = new[] {await GameFile(_host.GameFolder, "Data/Skyrim.esm", "esm bytes")};

        var result = await _check.Run(await Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("incomplete", result.Message);
        Assert.Contains("SkyrimSE.exe", result.Message);
        Assert.Empty(_progress.Archives);
    }

    /// <summary>
    ///     This check owns RepairableGameFiles, so every exit from it has to leave that true - including the
    ///     one that gives up before looking at a single archive. A stale list would have the action fetching
    ///     files for a game folder that has since broken in a different way.
    /// </summary>
    [Fact]
    public async Task AnIncompleteGameInstallClearsWhatAnEarlierRunFoundToRepair()
    {
        _host.Config.ModList.Archives = new[] {await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")};
        var ctx = await Context();

        var first = await _check.Run(ctx, _progress, CancellationToken.None);
        Assert.Equal(PreflightState.Failed, first.State);
        Assert.NotEmpty(ctx.State.RepairableGameFiles);

        _host.GameFolder.Combine("SkyrimSE.exe").Delete();
        var second = await _check.Run(ctx, new RecordingProgress(), CancellationToken.None);

        Assert.Contains("incomplete", second.Message);
        Assert.Empty(ctx.State.RepairableGameFiles);
    }

    [Fact]
    public async Task OtherGameFilesComeFromTheOtherGameFolder()
    {
        var fallout = _host.Manager.CreateFolder().Path;
        var archive = await GameFile(fallout, "Data/Fallout4.esm", "fo4 bytes", Game.Fallout4);
        _host.Config.ModList.Archives = new[] {archive};
        var ctx = _host.Context();
        ctx.State.GameFolder = _host.GameFolder;
        ctx.State.OtherGameFolders[Game.Fallout4] = fallout;
        await _host.Inventory(ctx);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);
        Assert.Equal(PreflightState.Passed, result.State);

        ctx.State.OtherGameFolders.Clear();
        await _host.Inventory(ctx);
        var withoutFolder = await _check.Run(ctx, new RecordingProgress(), CancellationToken.None);
        Assert.Equal(PreflightState.Failed, withoutFolder.State);
        Assert.Contains("missing", withoutFolder.Message);
    }

    [Fact]
    public async Task TheMessageNamesAtMostTwentyFilesAndTheDetailAllOfThem()
    {
        var archives = new List<Archive>();
        for (var i = 0; i < 25; i++)
            archives.Add(await GameFile(null, $"Data/missing{i:00}.esm", $"bytes {i}"));
        _host.Config.ModList.Archives = archives.ToArray();

        var result = await _check.Run(await Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("and 5 more", result.Message);
        Assert.DoesNotContain("missing24", result.Message);
        Assert.Equal(25, archives.Count(a => result.Detail!.Contains(a.Name)));
    }

    /// <summary>
    ///     The repair is offered, never taken: the row says it is available and a host has to ask before
    ///     anything happens. A copy of the game that did not come from Steam gets no offer at all, because
    ///     the offer would end in "your account owns none of this".
    /// </summary>
    [Fact]
    public async Task OffersTheRepairOnlyForAGameThatCameFromSteam()
    {
        _host.Restorer = new FakeGameFileRestorer();
        _host.Config.ModList.Archives = new[] {await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")};

        var withoutSteam = await _check.Run(await Context(), _progress, CancellationToken.None);
        Assert.Null(withoutSteam.Actions);

        _host.Locator.SteamBuildIds[Game.SkyrimSpecialEdition] = "1234567";
        var withSteam = await _check.Run(await Context(), new RecordingProgress(), CancellationToken.None);

        Assert.Equal(PreflightState.Failed, withSteam.State);
        Assert.Equal(new[] {PreflightAction.RepairGameFiles}, withSteam.Actions);
        Assert.Contains("Steam can fetch 1 file", withSteam.Detail);
    }

    /// <summary>
    ///     A game that is not installed at all: game-installed left no folder and said the source can supply
    ///     the game, so every file this list takes from it is missing rather than mismatched - nothing here
    ///     is the wrong version of anything - and the repair is offered.
    ///     <para>
    ///         The Steam build id is what says "this came from a store we can fetch from" for an installed
    ///         game, and it is read out of a local install. There is none, so <c>SourcedGames</c> stands in
    ///         its place; without that this row would list every game file as missing and offer no way to
    ///         get any of them.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task AGameThatIsSourcedRatherThanInstalledHasEveryFileMissing()
    {
        _host.Restorer = new FakeGameFileRestorer();
        _host.Config.ModList.Archives = new[]
        {
            await GameFile(null, "Data/Skyrim.esm", "esm bytes"),
            await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")
        };

        var ctx = _host.Context();
        ctx.State.GameFolder = default;
        ctx.State.SourcedGames.Add(Game.SkyrimSpecialEdition);
        await _host.Inventory(ctx);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("2 game files are missing", result.Message);
        Assert.Equal(new[] {PreflightAction.RepairGameFiles}, result.Actions);
        Assert.All(ctx.State.RepairableGameFiles, f => Assert.Equal(GameFileProblem.Missing, f.Problem));
    }

    /// <summary>
    ///     The game's own required files are a claim about an install, and there is none. Saying the install
    ///     at "" is incomplete would be both untrue and a row the user has already seen - the Warning that
    ///     let the run get this far.
    /// </summary>
    [Fact]
    public async Task ASourcedGameIsNotCheckedForRequiredFiles()
    {
        _host.Restorer = new FakeGameFileRestorer();
        _host.Config.ModList.Archives = Array.Empty<Archive>();

        var ctx = _host.Context();
        ctx.State.GameFolder = default;
        ctx.State.SourcedGames.Add(Game.SkyrimSpecialEdition);
        await _host.Inventory(ctx);

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("takes no files from the game", result.Message);
    }

    /// <summary>
    ///     A user who has not logged into Steam is told what logging in would get them, on a row that still
    ///     offers the action. What must not happen is a login starting because a check ran.
    /// </summary>
    [Fact]
    public async Task SaysWhatALoginWouldBuyWhenThereIsNone()
    {
        _host.Restorer = new FakeGameFileRestorer
            {Ready = false, NotReadyReason = "Log into Steam and these can be fetched for you."};
        _host.Locator.SteamBuildIds[Game.SkyrimSpecialEdition] = "1234567";
        _host.Config.ModList.Archives = new[] {await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")};

        var result = await _check.Run(await Context(), _progress, CancellationToken.None);

        Assert.Equal(new[] {PreflightAction.RepairGameFiles}, result.Actions);
        Assert.Contains("Log into Steam and these can be fetched for you.", result.Detail);
    }

    /// <summary>
    ///     What a repair would do beyond downloading is asked about the games of the files being repaired,
    ///     and not about the modlist's own game.
    ///     <para>
    ///         For nearly every list those are the same set, which is what makes the difference easy to get
    ///         wrong: a list with <c>CanSourceFrom</c> files carries a second game, and asking about
    ///         <c>Config.Game</c> alone would miss a companion app for the sourced game while naming one for
    ///         a game whose files are all fine. Both directions are pinned here.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task AsksAboutTheGamesOfTheFilesBeingRepaired()
    {
        const string consequence = "This adds a free Fallout 4 app to your Steam library.";

        _host.Restorer = new FakeGameFileRestorer
        {
            Consequence = consequence,
            ConsequentialGames = {Game.Fallout4}
        };
        _host.Locator.SteamBuildIds[Game.SkyrimSpecialEdition] = "1234567";

        // Only the modlist's own game is short a file, and it has no companion app.
        _host.Config.ModList.Archives = new[] {await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")};

        var skyrimOnly = await _check.Run(await Context(), _progress, CancellationToken.None);

        Assert.Equal(new[] {PreflightAction.RepairGameFiles}, skyrimOnly.Actions);
        Assert.DoesNotContain(consequence, skyrimOnly.Detail);

        // Now a file sourced from a second game, which does. Nothing about the modlist's GameType changed.
        _host.Config.ModList.Archives = new[]
        {
            await GameFile(null, "Data/Dawnguard.esm", "dlc bytes"),
            await GameFile(null, "Data/Fallout4.esm", "other game bytes", Game.Fallout4)
        };

        var bothGames = await _check.Run(await Context(), new RecordingProgress(), CancellationToken.None);

        Assert.Equal(new[] {PreflightAction.RepairGameFiles}, bothGames.Actions);
        Assert.Contains(consequence, bothGames.Detail);
    }

    /// <summary>A host with no restorer at all keeps the behaviour it has always had: fix it by hand.</summary>
    [Fact]
    public async Task OffersNothingWhenThereIsNoRestorer()
    {
        _host.Locator.SteamBuildIds[Game.SkyrimSpecialEdition] = "1234567";
        _host.Config.ModList.Archives = new[] {await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")};

        var result = await _check.Run(await Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Null(result.Actions);
    }
}
