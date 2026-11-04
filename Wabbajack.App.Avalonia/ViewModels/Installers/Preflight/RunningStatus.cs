using System;
using System.Collections.Generic;
using Wabbajack.Common;

namespace Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

/// <summary>
///     The two lines preflight's detail panel shows under a check while it runs, so a long check reads as alive:
///     how far it has got, and how fast and how long. Counts only, never a list, however many files there are.
/// </summary>
public static class RunningStatus
{
    /// <summary>How long a phase has to have run before a time-left estimate means anything.</summary>
    private static readonly TimeSpan EstimateAfter = TimeSpan.FromSeconds(5);

    /// <param name="current">How many the check has done in this phase.</param>
    /// <param name="total">How many there are in this phase; zero when the check has no count to give.</param>
    /// <param name="elapsed">How long the check has been running.</param>
    /// <param name="inPhase">How long this phase has been running, which the estimate is based on.</param>
    /// <param name="bytesPerSecond">What is being hashed right now, per second.</param>
    public static (string Count, string Stats) Describe(long current, long total, TimeSpan elapsed, TimeSpan inPhase,
        long bytesPerSecond)
    {
        var count = total > 0
            ? $"{current:N0} of {total:N0} ({(int) Math.Floor(100.0 * Math.Min(current, total) / total)}%)"
            : string.Empty;

        var stats = new List<string>();
        if (bytesPerSecond > 0)
            stats.Add($"Reading {bytesPerSecond.ToFileSizeString()}/s");
        stats.Add($"{Clock(elapsed)} elapsed");
        if (total > 0 && current > 0 && current < total && inPhase >= EstimateAfter)
            stats.Add(Left(TimeSpan.FromTicks((long) (inPhase.Ticks * ((double) (total - current) / current)))));

        return (count, string.Join(" · ", stats));
    }

    private static string Clock(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int) t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";

    private static string Left(TimeSpan t)
    {
        if (t < TimeSpan.FromMinutes(1)) return "less than a minute left";
        if (t < TimeSpan.FromHours(1)) return $"about {(int) Math.Ceiling(t.TotalMinutes)} min left";
        return $"about {(int) t.TotalHours} h {t.Minutes} min left";
    }
}
