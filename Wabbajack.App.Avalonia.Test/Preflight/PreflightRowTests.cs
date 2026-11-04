using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Preflight;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Preflight;

/// <summary>
///     What the preflight screen shows for a check and for each archive: the row's status text and button, which
///     band a download sits in, and the running counts and time left. All of it is a projection of the runner's
///     state, so these build that state directly.
/// </summary>
public class PreflightRowTests
{
    private static CheckStatus Status(string id, PreflightState state, string message = "message",
        bool acknowledged = false, string? progressText = null, params PreflightAction[] actions)
    {
        return new CheckStatus(id, "A check", state, message, null, actions, Percent.Zero, progressText, acknowledged);
    }

    private static PreflightCheckVM Row(CheckStatus status) =>
        new(status, (_, _, _) => Task.CompletedTask, CancellationToken.None);

    // ---- the checklist row ----

    [Theory]
    [InlineData(PreflightState.Pending, "waiting")]
    [InlineData(PreflightState.Skipped, "skipped")]
    [InlineData(PreflightState.Cancelled, "stopped")]
    [InlineData(PreflightState.Passed, "message")]
    [InlineData(PreflightState.Failed, "message")]
    public void StatusText_FollowsTheState(PreflightState state, string expected)
    {
        Assert.Equal(expected, Row(Status("disk-space", state)).StatusText);
    }

    [Fact]
    public void RunningCheck_ShowsWhatItIsDoing()
    {
        var row = Row(Status("archive-inventory", PreflightState.Running, progressText: "Hashing downloads"));

        Assert.Equal("Hashing downloads", row.StatusText);
        Assert.Equal("Hashing downloads", row.Message);
        Assert.NotNull(row.RunningSince);
        Assert.Equal("running", Row(Status("archive-inventory", PreflightState.Running)).StatusText);
    }

    /// <summary>A warning only stops standing in the way once the user has accepted it, and then says so.</summary>
    [Fact]
    public void Warning_IsSatisfiedOnlyOnceAccepted()
    {
        var row = Row(Status("disk-space", PreflightState.Warning, "Low on space"));
        Assert.False(row.IsSatisfied);
        Assert.Equal("Low on space", row.StatusText);

        row.Apply(Status("disk-space", PreflightState.Warning, "Low on space", acknowledged: true));

        Assert.True(row.IsSatisfied);
        Assert.Equal("Low on space (accepted)", row.StatusText);
    }

    /// <summary>A check the user stopped offers to resume it even though the check itself offered nothing.</summary>
    [Fact]
    public void StoppedCheck_OffersResume()
    {
        var row = Row(Status("automated-downloads", PreflightState.Cancelled));

        Assert.True(row.HasAction);
        Assert.Equal("Resume", row.ActionLabel);
    }

    [Fact]
    public void Action_IsTheChecksFirst()
    {
        var row = Row(Status("nexus-login", PreflightState.NeedsUser, actions: new[]
        {
            new PreflightAction("log-in", "Log in"), new PreflightAction("skip", "Skip")
        }));

        Assert.Equal("Log in", row.ActionLabel);
        Assert.False(Row(Status("disk-space", PreflightState.Passed)).HasAction);
    }

    [Theory]
    [InlineData(PreflightCheckIds.AutomatedDownloads, false, PreflightDetailKind.BulkDownloads)]
    [InlineData(PreflightCheckIds.ManualDownloads, false, PreflightDetailKind.ManualDownloads)]
    [InlineData(PreflightCheckIds.GameFiles, true, PreflightDetailKind.GameFiles)]
    [InlineData(PreflightCheckIds.GameFiles, false, PreflightDetailKind.None)]
    [InlineData("disk-space", false, PreflightDetailKind.None)]
    public void DetailPanel_GameFilesGetsItsCardOnlyWhenItCanRepair(string id, bool offersRepair,
        PreflightDetailKind expected)
    {
        var actions = offersRepair ? new[] {PreflightAction.RepairGameFiles} : Array.Empty<PreflightAction>();

        Assert.Equal(expected, Row(Status(id, PreflightState.NeedsUser, actions: actions)).DetailKind);
    }

    /// <summary>The time-left estimate restarts with each phase a check counts through.</summary>
    [Fact]
    public void Phase_RestartsWhenWhatTheCheckIsDoingChanges()
    {
        var row = Row(Status("archive-inventory", PreflightState.Running, progressText: "Checking the install"));
        var firstPhase = row.PhaseSince;
        Thread.Sleep(20);

        row.Apply(Status("archive-inventory", PreflightState.Running, progressText: "Checking the install"));
        Assert.Equal(firstPhase, row.PhaseSince);

        row.Apply(Status("archive-inventory", PreflightState.Running, progressText: "Hashing downloads"));
        Assert.NotEqual(firstPhase, row.PhaseSince);

        row.Apply(Status("archive-inventory", PreflightState.Passed));
        Assert.Null(row.RunningSince);
        Assert.Null(row.PhaseSince);
    }

    // ---- archive rows ----

    private static Archive Archive(IDownloadState state, long size = 1000) =>
        new() {Name = "Mod.7z", Size = size, Hash = Hash.FromULong(1), State = state};

    private static ArchiveRowVM ArchiveRow(long size = 1000) =>
        new(Archive(new Http {Url = new Uri("https://example.com/Mod.7z")}, size), null);

    [Theory]
    [InlineData(ArchiveState.Downloading, ArchiveGroup.Current)]
    [InlineData(ArchiveState.ManualInProgress, ArchiveGroup.Current)]
    [InlineData(ArchiveState.Failed, ArchiveGroup.Current)]
    [InlineData(ArchiveState.Unsupported, ArchiveGroup.Current)]
    [InlineData(ArchiveState.Missing, ArchiveGroup.Remaining)]
    [InlineData(ArchiveState.ManualRequired, ArchiveGroup.Remaining)]
    [InlineData(ArchiveState.Present, ArchiveGroup.Done)]
    [InlineData(ArchiveState.Downloaded, ArchiveGroup.Done)]
    public void Band_KeepsFailuresWithWhatIsHappeningNow(ArchiveState state, ArchiveGroup band)
    {
        var row = ArchiveRow();
        row.Apply(new ArchiveStatus(Archive(new Http()), state, null, null, null));

        Assert.Equal(band, row.Group);
        Assert.Equal(band == ArchiveGroup.Done, row.IsDone);
    }

    [Theory]
    [InlineData(ArchiveState.Downloading, 250L, 0.25)]
    [InlineData(ArchiveState.Downloading, 5000L, 1.0)]
    [InlineData(ArchiveState.Downloading, null, 0.0)]
    [InlineData(ArchiveState.Downloaded, null, 1.0)]
    [InlineData(ArchiveState.Missing, 500L, 0.0)]
    public void Progress_IsTheShareDownloadedAndNeverOverflows(ArchiveState state, long? bytes, double expected)
    {
        var row = ArchiveRow(size: 1000);

        row.Apply(new ArchiveStatus(Archive(new Http()), state, "detail", bytes, null));

        Assert.Equal(expected, row.Progress, 3);
        Assert.Equal("detail", row.Detail);
    }

    [Theory]
    [InlineData(ManualDownloadState.Pending, ArchiveState.ManualRequired)]
    [InlineData(ManualDownloadState.Detected, ArchiveState.ManualInProgress)]
    [InlineData(ManualDownloadState.Waiting, ArchiveState.ManualInProgress)]
    [InlineData(ManualDownloadState.Verifying, ArchiveState.ManualInProgress)]
    [InlineData(ManualDownloadState.Moved, ArchiveState.Present)]
    [InlineData(ManualDownloadState.WrongFile, ArchiveState.Failed)]
    [InlineData(ManualDownloadState.Failed, ArchiveState.Failed)]
    public void ManualDownload_MapsOntoTheSameStates(ManualDownloadState manual, ArchiveState expected)
    {
        var row = ArchiveRow();

        row.Apply(new ManualDownloadItem("Mod.7z", Archive(new Http()), 3, manual, null, null,
            Percent.FactoryPutInRange(0.5), null));

        Assert.Equal(expected, row.State);
        Assert.Equal(3, row.Order);
    }

    [Theory]
    [InlineData("Game files")]
    [InlineData("Nexus Mods")]
    [InlineData("Direct download")]
    [InlineData("Wabbajack CDN")]
    public void Source_IsNamedForPeople(string expected)
    {
        IDownloadState state = expected switch
        {
            "Game files" => new GameFileSource(),
            "Nexus Mods" => new Nexus(),
            "Direct download" => new Http(),
            _ => new WabbajackCDN()
        };

        Assert.Equal(expected, new ArchiveRowVM(Archive(state), null).SourceName);
    }

    // ---- running counts and time left ----

    [Fact]
    public void Running_CountsAndPercentage()
    {
        var (count, stats) = RunningStatus.Describe(250, 1000, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), 0);

        Assert.Equal($"{250:N0} of {1000:N0} (25%)", count);
        Assert.Equal("0:03 elapsed", stats);
    }

    [Fact]
    public void Running_NoTotal_ShowsNoCount()
    {
        Assert.Equal(string.Empty, RunningStatus.Describe(5, 0, TimeSpan.Zero, TimeSpan.Zero, 0).Count);
    }

    /// <summary>An estimate from the first few seconds is noise, so none is given until the phase has run a while.</summary>
    [Theory]
    [InlineData(4, null)]
    [InlineData(30, "about 2 min left")]
    [InlineData(100, "about 5 min left")]
    [InlineData(2000, "about 1 h 40 min left")]
    public void Running_EstimatesTimeLeftOnlyAfterAFewSeconds(int phaseSeconds, string? expected)
    {
        // A quarter done, so three times the phase so far is left.
        var stats = RunningStatus.Describe(250, 1000, TimeSpan.FromSeconds(phaseSeconds),
            TimeSpan.FromSeconds(phaseSeconds), 0).Stats;

        if (expected == null)
            Assert.DoesNotContain("left", stats);
        else
            Assert.EndsWith(expected, stats);
    }

    [Fact]
    public void Running_NearlyDone_IsLessThanAMinute()
    {
        Assert.EndsWith("less than a minute left",
            RunningStatus.Describe(990, 1000, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), 0).Stats);
    }

    [Fact]
    public void Running_LongRuns_ShowHours()
    {
        Assert.StartsWith("1:01:05 elapsed",
            RunningStatus.Describe(0, 0, TimeSpan.FromSeconds(3665), TimeSpan.Zero, 0).Stats);
    }

    [Fact]
    public void Running_ShowsTheReadRateFirst()
    {
        Assert.StartsWith("Reading ", RunningStatus.Describe(1, 10, TimeSpan.FromSeconds(1), TimeSpan.Zero, 50_000_000).Stats);
    }
}
