using System.Collections.Generic;
using System.Linq;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Installers;

/// <summary>When "open the next one automatically" opens a page, and which one.</summary>
public class AutoOpenPlannerTests
{
    private static ManualDownloadItem Item(string key, int order, ManualDownloadState state = ManualDownloadState.Pending)
    {
        return new ManualDownloadItem(key, new Archive {Name = key}, order, state, null, null, Percent.Zero, null);
    }

    private static List<ManualDownloadItem> Queue(params (string Key, ManualDownloadState State)[] items)
    {
        return items.Select((i, n) => Item(i.Key, n, i.State)).ToList();
    }

    [Fact]
    public void NothingOpensUntilTheUserOpensAPage()
    {
        var planner = new AutoOpenPlanner();

        Assert.Null(planner.Next(Queue(("a", ManualDownloadState.Pending), ("b", ManualDownloadState.Pending))));
    }

    [Fact]
    public void WaitsWhileTheOpenedPageHasNoFileArrivingYet()
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");

        Assert.Null(planner.Next(Queue(("a", ManualDownloadState.Pending), ("b", ManualDownloadState.Pending))));
    }

    [Theory]
    [InlineData(ManualDownloadState.Detected)]
    [InlineData(ManualDownloadState.Waiting)]
    [InlineData(ManualDownloadState.Verifying)]
    [InlineData(ManualDownloadState.Moved)]
    public void OpensTheNextOnceTheFileIsArriving(ManualDownloadState state)
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");

        Assert.Equal("b", planner.Next(Queue(("a", state), ("b", ManualDownloadState.Pending),
            ("c", ManualDownloadState.Pending))));
    }

    [Fact]
    public void NeverOpensTheSamePageTwice()
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");
        planner.Opened("b");

        Assert.Equal("c", planner.Next(Queue(("a", ManualDownloadState.Moved), ("b", ManualDownloadState.Detected),
            ("c", ManualDownloadState.Pending))));
    }

    /// <summary>Two pages waiting on the user is one too many; the user may have walked away.</summary>
    [Fact]
    public void OnlyOnePageWaitsOnTheUserAtATime()
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");
        planner.Opened("b");

        Assert.Null(planner.Next(Queue(("a", ManualDownloadState.Moved), ("b", ManualDownloadState.Pending),
            ("c", ManualDownloadState.Pending))));
    }

    [Theory]
    [InlineData(ManualDownloadState.WrongFile)]
    [InlineData(ManualDownloadState.Failed)]
    public void StopsWhenAFileNeedsTheUser(ManualDownloadState state)
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");

        Assert.Null(planner.Next(Queue(("a", state), ("b", ManualDownloadState.Pending))));
    }

    [Fact]
    public void ASkippedFileNoLongerHoldsThingsUp()
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");
        planner.Released("a");

        Assert.Equal("b", planner.Next(Queue(("b", ManualDownloadState.Pending), ("a", ManualDownloadState.Pending))));
    }

    [Fact]
    public void FollowsTheQueueOrderNotTheListOrder()
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");
        var items = new List<ManualDownloadItem>
        {
            Item("a", 0, ManualDownloadState.Detected), Item("late", 9), Item("next", 1)
        };

        Assert.Equal("next", planner.Next(items));
    }

    [Fact]
    public void NothingLeftMeansNothingToOpen()
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");

        Assert.Null(planner.Next(Queue(("a", ManualDownloadState.Moved))));
    }

    [Fact]
    public void OpeningASkippedFileAgainMakesItCountAgain()
    {
        var planner = new AutoOpenPlanner();
        planner.Opened("a");
        planner.Released("a");
        planner.Opened("a");

        Assert.Null(planner.Next(Queue(("a", ManualDownloadState.Pending), ("b", ManualDownloadState.Pending))));
    }
}
