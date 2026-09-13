using System;
using System.Collections.Generic;

namespace Wabbajack.Installer.Preflight;

public record PreflightResult(PreflightState State, string Message, string? Detail = null,
    IReadOnlyList<PreflightAction>? Actions = null, Exception? Error = null)
{
    public static PreflightResult Passed(string message, string? detail = null)
    {
        return new PreflightResult(PreflightState.Passed, message, detail);
    }

    public static PreflightResult Warning(string message, string? detail = null, PreflightAction[]? actions = null)
    {
        return new PreflightResult(PreflightState.Warning, message, detail, actions);
    }

    public static PreflightResult Failed(string message, string? detail = null, PreflightAction[]? actions = null,
        Exception? error = null)
    {
        return new PreflightResult(PreflightState.Failed, message, detail, actions, error);
    }

    public static PreflightResult NeedsUser(string message, string? detail = null, PreflightAction[]? actions = null)
    {
        return new PreflightResult(PreflightState.NeedsUser, message, detail, actions);
    }
}
