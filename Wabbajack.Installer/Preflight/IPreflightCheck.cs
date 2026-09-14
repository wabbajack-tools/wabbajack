using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     One row on the preflight checklist. Checks are stateless and registered in DI; anything a check learns
///     that a later check needs goes on <see cref="PreflightContext.State" />.
/// </summary>
public interface IPreflightCheck
{
    string Id { get; }
    string Title { get; }

    /// <summary>
    ///     Checks run one at a time, lowest first. A dependency must have a lower order than every check that
    ///     depends on it; the runner refuses a set of checks where that does not hold.
    /// </summary>
    int Order { get; }

    /// <summary>
    ///     Ids of checks that must have Passed (or ended in a Warning) before this one runs. When one of them
    ///     Failed, needs the user or was cancelled, this check is marked Skipped instead of running.
    /// </summary>
    IReadOnlyList<string> DependsOn { get; }

    /// <summary>
    ///     Whether a <see cref="PreflightState.NeedsUser" /> result from this check stops the run where it
    ///     stands. True for a check that has found something the user must go and fix - a login, a missing
    ///     game - because everything after it is being measured against an install that is not the one they
    ///     will get, and a checklist that keeps going hands them a page of consequences instead of the one
    ///     thing to do.
    ///     <para>
    ///         The download checks set this false. NeedsUser is their ordinary ending: they have just handed
    ///         the user a queue of files to fetch, which is work to do rather than a mistake to correct, and
    ///         the checks after them still have something true to say.
    ///     </para>
    /// </summary>
    bool NeedsUserStopsRun => true;

    Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token);
}

public static class PreflightCheckIds
{
    public const string NexusLogin = "nexus-login";
    public const string GameInstalled = "game-installed";
    public const string GameFiles = "game-files";
    public const string ArchiveInventory = "archive-inventory";
    public const string UnsupportedArchives = "unsupported-archives";
    public const string AutomatedDownloads = "automated-downloads";
    public const string ManualDownloads = "manual-downloads";
    public const string DiskSpace = "disk-space";
}
