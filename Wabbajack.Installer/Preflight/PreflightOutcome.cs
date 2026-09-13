using System.Collections.Generic;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     <see cref="Ready" /> is true only when every check Passed or is a Warning the user acknowledged.
/// </summary>
public record PreflightOutcome(bool Ready, IReadOnlyList<CheckStatus> Checks);
