using System.Linq;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Rules;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Preflight;

/// <summary>
///     The one line the game-files card shows after a repair, and how each file is described before one. Failures
///     are counted by reason rather than listed: they almost always share one sentence, and naming a single file
///     made it read like a problem with that file.
/// </summary>
public class GameFilesSummaryTests
{
    private static GameFileRepairResult Result(GameFileRepairStatus status, string message = "") =>
        new(new Archive {Name = "Skyrim.esm"}, status, message);

    private static GameFileRepairResult[] Results(int repaired, params (int Count, string Message)[] failures) =>
        Enumerable.Repeat(Result(GameFileRepairStatus.Repaired), repaired)
            .Concat(failures.SelectMany(f => Enumerable.Repeat(Result(GameFileRepairStatus.Failed, f.Message), f.Count)))
            .ToArray();

    [Theory]
    [InlineData(1, "1 file fetched into the downloads folder.")]
    [InlineData(12, "12 files fetched into the downloads folder.")]
    public void AllFetched(int count, string expected)
    {
        Assert.Equal((GameFilesTone.Done, expected), GameFilesVM.Summary(Results(count)));
    }

    [Fact]
    public void NoneFetched_ForOneReason()
    {
        Assert.Equal((GameFilesTone.Problem, "4 files could not be fetched. Nobody has indexed 1.6.1170."),
            GameFilesVM.Summary(Results(0, (4, "Nobody has indexed 1.6.1170."))));
    }

    [Fact]
    public void SomeFetched_ShowsTheCommonestReasonAndHowManyItCovers()
    {
        var (tone, text) = GameFilesVM.Summary(Results(2, (1, "Steam refused."), (3, "Nobody has indexed 1.6.1170.")));

        Assert.Equal(GameFilesTone.Problem, tone);
        Assert.Equal("2 of 6 fetched; 4 files could not be. (3 of 4) Nobody has indexed 1.6.1170.", text);
    }

    [Theory]
    [InlineData(GameFileProblem.Missing, null, ArchiveState.Missing, "Not installed")]
    [InlineData(GameFileProblem.Mismatched, null, ArchiveState.Failed, "Another version")]
    [InlineData(GameFileProblem.Mismatched, "1.5.97.0", ArchiveState.Failed, "Another version; this list needs 1.5.97.0")]
    public void Row_DescribesWhatIsWrongWithTheFile(GameFileProblem problem, string? version, ArchiveState state,
        string detail)
    {
        Assert.Equal((state, detail), GameFilesVM.Row(problem, version));
    }
}
