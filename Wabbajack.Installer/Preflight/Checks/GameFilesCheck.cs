using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Verifies every archive the list takes from the game itself, the way <c>GameFileDownloader.Verify</c>
///     does, against the folders game-installed resolved. Also confirms the game's declared RequiredFiles are
///     present. Game files are never routed to a manual download: a mismatch means the wrong game version.
/// </summary>
public sealed class GameFilesCheck : IPreflightCheck
{
    private const int MaxNamesInMessage = 20;

    public string Id => PreflightCheckIds.GameFiles;
    public string Title => "Game files";
    public int Order => 300;
    public IReadOnlyList<string> DependsOn => new[] {PreflightCheckIds.GameInstalled};

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var game = ctx.Config.Game;
        var folder = ctx.State.GameFolder;
        var meta = game.MetaData();

        var missingRequired = meta.RequiredFiles
            .Where(r => !folder.Combine(r).FileExists())
            .ToList();
        if (missingRequired.Count > 0)
        {
            return PreflightResult.Failed(
                $"The {meta.HumanFriendlyGameName} install at \"{folder}\" is incomplete: " +
                $"{Summarise(missingRequired.Select(r => r.ToString()))}",
                string.Join(Environment.NewLine, missingRequired));
        }

        var gameFiles = ctx.ModList.Archives.Where(a => a.State is GameFileSource).ToArray();
        if (gameFiles.Length == 0)
            return PreflightResult.Passed("This list takes no files from the game");

        var done = 0L;
        progress.Report(0, gameFiles.Length);

        var results = await gameFiles.PMapAll(ctx.Limiter, async archive =>
        {
            var state = (GameFileSource) archive.State;
            var outcome = await Verify(ctx, archive, state, token);
            progress.Report(Interlocked.Increment(ref done), gameFiles.Length);
            progress.Archive(archive, outcome switch
            {
                Outcome.Verified => ArchiveState.Present,
                Outcome.Missing => ArchiveState.Missing,
                _ => ArchiveState.Failed
            }, outcome switch
            {
                Outcome.Missing => "Not found in the game folder",
                Outcome.Mismatch => "Does not match the version this list was built against",
                _ => null
            });
            return (archive, state, outcome);
        }).ToList();

        var missing = results.Where(r => r.outcome == Outcome.Missing).Select(r => r.archive).ToList();
        var mismatched = results.Where(r => r.outcome == Outcome.Mismatch).ToList();

        if (missing.Count == 0 && mismatched.Count == 0)
            return PreflightResult.Passed($"{gameFiles.Length} game files verified");

        var parts = new List<string>();
        var detail = new List<string>();

        if (mismatched.Count > 0)
        {
            var expected = string.Join(", ",
                mismatched.Select(m => m.state.GameVersion).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct());
            var actual = GameVersionDetector.Detect(game, folder, ctx.GameLocator, ctx.Logger);
            var versions = expected.Length > 0
                ? $"built against {expected}; you have {actual ?? "an unknown version"}"
                : $"you have {actual ?? "an unknown version"}";
            parts.Add($"{mismatched.Count} game files don't match ({versions}): " +
                      Summarise(mismatched.Select(m => m.archive.Name)));
            detail.Add("Mismatched:");
            detail.AddRange(mismatched.Select(m => "  " + m.archive.Name));
        }

        if (missing.Count > 0)
        {
            parts.Add($"{missing.Count} game files are missing (missing DLC or a modified install?): " +
                      Summarise(missing.Select(m => m.Name)));
            detail.Add("Missing:");
            detail.AddRange(missing.Select(m => "  " + m.Name));
        }

        return PreflightResult.Failed(string.Join(" ", parts), string.Join(Environment.NewLine, detail));
    }

    private static async Task<Outcome> Verify(PreflightContext ctx, Archive archive, GameFileSource state,
        CancellationToken token)
    {
        AbsolutePath root;
        if (state.Game == ctx.Config.Game)
            root = ctx.State.GameFolder;
        else if (!ctx.State.OtherGameFolders.TryGetValue(state.Game, out root))
            return Outcome.Missing;

        var fp = state.GameFile.RelativeTo(root);
        if (!fp.FileExists()) return Outcome.Missing;
        return await ctx.HashCache.FileHashCachedAsync(fp, token) == archive.Hash
            ? Outcome.Verified
            : Outcome.Mismatch;
    }

    private static string Summarise(IEnumerable<string> names)
    {
        var list = names.ToList();
        var shown = string.Join(", ", list.Take(MaxNamesInMessage));
        return list.Count > MaxNamesInMessage ? $"{shown} and {list.Count - MaxNamesInMessage} more" : shown;
    }

    private enum Outcome
    {
        Verified,
        Missing,
        Mismatch
    }
}
