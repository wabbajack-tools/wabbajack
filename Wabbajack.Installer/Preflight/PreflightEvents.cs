using System.Collections.Generic;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.RateLimiter;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Immutable snapshot of one check as the runner sees it.
/// </summary>
public record CheckStatus(string Id, string Title, PreflightState State, string Message, string? Detail,
    IReadOnlyList<PreflightAction> Actions, Percent Progress, string? ProgressText, bool Acknowledged)
{
    /// <summary>
    ///     True when this check no longer stands in the way of installing: it Passed, or it ended in a Warning
    ///     the user has accepted.
    /// </summary>
    public bool IsSatisfied => State == PreflightState.Passed || (State == PreflightState.Warning && Acknowledged);
}

public abstract record PreflightEvent;

public sealed record CheckChanged(CheckStatus Status) : PreflightEvent;

public sealed record ArchiveChanged(ArchiveStatus Status) : PreflightEvent;

/// <summary>
///     The archives the user has to fetch by hand, in the order they will be asked for, each with the page
///     to open. Raised when manual-downloads starts and whenever the queue is republished.
/// </summary>
public sealed record ManualQueueChanged(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> Queue)
    : PreflightEvent;

public sealed record RunFinished(PreflightOutcome Outcome) : PreflightEvent;
