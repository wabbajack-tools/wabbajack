using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

/// <summary>
///     Fetches the game files a modlist needs and this machine's game install does not have.
///     Runs preflight as far as the game-files check, which is what works out - by hash, the way the
///     installer does - which of the list's <c>GameFileSource</c> archives are missing outright and which
///     are from another version of the game. Then, unless asked only to report, fetches each one and puts it
///     in the downloads folder. Nothing is written to the game install.
///     Where the files come from depends on the file. Steam's depots want a stored login, which
///     <c>steam-login</c> writes; Anniversary Edition Creations come from Bethesda and want the Steam client
///     running instead, with no Wabbajack login at all. Whichever is missing, the verb says what having it
///     would get and stops; it will not start a login in the middle of a script.
/// </summary>
public class RepairGameFiles
{
    private readonly DTOSerializer _dtos;
    private readonly ILogger<RepairGameFiles> _logger;
    private readonly IServiceProvider _provider;

    public RepairGameFiles(ILogger<RepairGameFiles> logger, DTOSerializer dtos, IServiceProvider provider)
    {
        _logger = logger;
        _dtos = dtos;
        _provider = provider;
    }

    public static VerbDefinition Definition = new VerbDefinition("repair-game-files",
        "Fetches the game files a modlist needs from the store the game came from", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "w", "wabbajack", "Wabbajack file"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Install path, so only what this install still needs is considered"),
            new OptionDefinition(typeof(AbsolutePath), "d", "downloads", "Downloads path, where fetched files are placed"),
            new OptionDefinition(typeof(bool), "n", "report-only", "Say what would be fetched and fetch nothing")
        });

    public async Task<int> Run(AbsolutePath wabbajack, AbsolutePath output, AbsolutePath downloads, bool reportOnly,
        CancellationToken token)
    {
        if (wabbajack == default || !wabbajack.FileExists())
        {
            _logger.LogError("--wabbajack is required and must name a .wabbajack file");
            return 1;
        }

        if (output == default || downloads == default)
        {
            _logger.LogError("--output and --downloads are both required: the first decides what this install " +
                             "still needs, the second is where fetched files go");
            return 1;
        }

        var modlist = await StandardInstaller.LoadFromFile(_dtos, wabbajack);

        var runner = PreflightRunner.Create(_provider, new InstallerConfiguration
        {
            Downloads = downloads,
            Install = output,
            ModList = modlist,
            Game = modlist.GameType,
            ModlistArchive = wabbajack,
            GameFolder = default
        }, new PreflightOptions {WaitForManualDownloads = false, SendMetrics = false});

        // Stops at the first check the user has to act on, which for a broken game install is game-files
        // itself. Anything earlier failing - no game folder - is reported and there is nothing to repair.
        await runner.RunAll(token);

        var ctx = runner.Context;
        var gameFiles = runner.Checks.FirstOrDefault(c => c.Id == PreflightCheckIds.GameFiles);

        if (gameFiles == null || gameFiles.State == PreflightState.Pending)
        {
            foreach (var check in runner.Checks.Where(c => !c.IsSatisfied && c.State != PreflightState.Pending))
                _logger.LogError("[{State}] {Title}: {Message}", check.State, check.Title, check.Message);
            _logger.LogError("Preflight stopped before the game files could be checked, so there is nothing to repair");
            return 1;
        }

        _logger.LogInformation("[{State}] {Title}: {Message}", gameFiles.State, gameFiles.Title, gameFiles.Message);

        var repairable = ctx.State.RepairableGameFiles;
        if (repairable.Count == 0)
        {
            _logger.LogInformation("Every game file this install needs is already accounted for");
            return 0;
        }

        Describe(repairable);

        // The same sentence the app puts in its offer, and before the same decision: a repair that has to
        // take a free tool licence puts that tool in the user's Steam library, and --report-only exists
        // precisely so somebody can read this before running the thing.
        foreach (var consequence in
                 ctx.GameFileRestorer?.Consequences(repairable.Select(r => r.State.Game))
                 ?? Array.Empty<string>())
            _logger.LogWarning("{Consequence}", consequence);

        if (reportOnly) return 0;

        var restorer = ctx.GameFileRestorer;
        var status = restorer?.Status();
        if (restorer == null || status is not {Ready: true})
        {
            // The reason above is every unready source's own sentence, so it already says what to do -
            // run steam-login, start Steam, install the game - and naming one of them here would be a
            // guess about which source this list actually needed.
            _logger.LogError("{Reason}", status?.Reason ?? "There is no way to fetch game files on this machine.");
            return 1;
        }

        _logger.LogInformation("Fetching from {Source}: {Reason}", restorer.SourceName, status.Reason);

        var results = await GameFileRepair.Run(ctx, repairable, new LoggingProgress(_logger), token);

        return Report(results, downloads);
    }

    /// <summary>What the check decided, before anything is fetched, so a report-only run is worth running.</summary>
    private void Describe(IReadOnlyList<RepairableGameFile> repairable)
    {
        foreach (var group in repairable.GroupBy(r => (r.State.Game, r.Version)))
        {
            _logger.LogInformation("{Game} {Version}:", group.Key.Game,
                group.Key.Version ?? "(no version recorded)");
            foreach (var item in group.OrderBy(i => i.Archive.Name, StringComparer.OrdinalIgnoreCase))
                _logger.LogInformation("  {Problem,-10} {Name} ({Size}) <- {GameFile}", item.Problem,
                    item.Archive.Name, item.Archive.Size.ToFileSizeString(), item.State.GameFile);
        }
    }

    private int Report(IReadOnlyList<GameFileRepairResult> results, AbsolutePath downloads)
    {
        var repaired = results.Where(r => r.Status == GameFileRepairStatus.Repaired).ToList();

        foreach (var result in repaired)
            _logger.LogInformation("Fetched {Name} at {Version} into {Downloads}", result.Archive.Name,
                result.VersionDescription, downloads);

        foreach (var result in results.Where(r => r.Status != GameFileRepairStatus.Repaired))
            _logger.LogError("[{Status}] {Name}: {Message}", result.Status, result.Archive.Name, result.Message);

        _logger.LogInformation("{Repaired} of {Total} game files fetched", repaired.Count, results.Count);
        return repaired.Count == results.Count ? 0 : 1;
    }

    /// <summary>
    ///     The repair reports through the same interface a check does, and outside a run there is no runner
    ///     to forward it to. Per-file progress is already logged by the repair itself, so this only carries
    ///     the archive transitions, which are the interesting ones on a console.
    /// </summary>
    private sealed class LoggingProgress : IPreflightProgress
    {
        private readonly ILogger _logger;

        public LoggingProgress(ILogger logger)
        {
            _logger = logger;
        }

        public void Report(long current, long total, string? text = null)
        {
        }

        public void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null)
        {
            if (state == ArchiveState.Downloading)
                _logger.LogInformation("{Name}: {Message}", archive.Name, message);
        }

        public void ManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue)
        {
        }
    }
}
