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
