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

    private PreflightContext Context()
    {
        var ctx = _host.Context();
        // What game-installed would have left behind.
        ctx.State.GameFolder = _host.GameFolder;
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
        var ctx = Context();

        var result = await _check.Run(ctx, _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("2 game files verified", result.Message);
        Assert.All(_progress.LastStates().Values, s => Assert.Equal(ArchiveState.Present, s));
        Assert.Contains(_progress.Reports, r => r.Current == 2 && r.Total == 2);
    }

    [Fact]
    public async Task PassesWhenTheListTakesNothingFromTheGame()
    {
        _host.Config.ModList.Archives = new[] {await PreflightTestHost.ArchiveFor("mod.7z", "not a game file")};
        var result = await _check.Run(Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Empty(_progress.Archives);
    }

    [Fact]
    public async Task AMissingGameFileFails()
    {
        _host.Config.ModList.Archives = new[]
        {
            await GameFile(_host.GameFolder, "Data/Skyrim.esm", "esm bytes"),
            await GameFile(null, "Data/Dawnguard.esm", "dlc bytes")
        };

        var result = await _check.Run(Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("1 game files are missing", result.Message);
        Assert.Contains("Data_Dawnguard.esm", result.Message);
        Assert.Equal(ArchiveState.Missing, _progress.LastStates()["Data_Dawnguard.esm"]);
        Assert.Equal(ArchiveState.Present, _progress.LastStates()["Data_Skyrim.esm"]);
        Assert.Null(result.Actions);
    }

    [Fact]
    public async Task AHashMismatchReportsTheVersionTheListWasBuiltAgainst()
    {
        var archive = await GameFile(null, "Data/Skyrim.esm", "expected bytes");
        await PreflightTestHost.WriteFile(_host.GameFolder.Combine("Data/Skyrim.esm"), "other bytes");
        _host.Config.ModList.Archives = new[] {archive};

        var result = await _check.Run(Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("1 game files don't match", result.Message);
        Assert.Contains("built against 1.6.640; you have an unknown version", result.Message);
        Assert.Equal(ArchiveState.Failed, _progress.LastStates()["Data_Skyrim.esm"]);
        Assert.Contains("Mismatched:", result.Detail);
    }

    [Fact]
    public async Task MissingRequiredFilesFailBeforeAnyHashing()
    {
        _host.GameFolder.Combine("SkyrimSE.exe").Delete();
        _host.Config.ModList.Archives = new[] {await GameFile(_host.GameFolder, "Data/Skyrim.esm", "esm bytes")};

        var result = await _check.Run(Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("incomplete", result.Message);
        Assert.Contains("SkyrimSE.exe", result.Message);
        Assert.Empty(_progress.Archives);
    }

    [Fact]
    public async Task OtherGameFilesComeFromTheOtherGameFolder()
    {
        var fallout = _host.Manager.CreateFolder().Path;
        var archive = await GameFile(fallout, "Data/Fallout4.esm", "fo4 bytes", Game.Fallout4);
        _host.Config.ModList.Archives = new[] {archive};
        var ctx = Context();
        ctx.State.OtherGameFolders[Game.Fallout4] = fallout;

        var result = await _check.Run(ctx, _progress, CancellationToken.None);
        Assert.Equal(PreflightState.Passed, result.State);

        ctx.State.OtherGameFolders.Clear();
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

        var result = await _check.Run(Context(), _progress, CancellationToken.None);

        Assert.Equal(PreflightState.Failed, result.State);
        Assert.Contains("and 5 more", result.Message);
        Assert.DoesNotContain("missing24", result.Message);
        Assert.Equal(25, archives.Count(a => result.Detail!.Contains(a.Name)));
    }
}
