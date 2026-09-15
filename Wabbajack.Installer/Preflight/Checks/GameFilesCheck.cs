using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Verifies every archive this install takes from the game itself, and confirms the game's declared
///     RequiredFiles are present.
///     <para>
///         It asks the question the installer asks: not "is this file at this path in the game folder", but
///         "is there anything, anywhere the installer looks, with these bytes". <c>AInstaller.HashArchives</c>
///         flattens the downloads folder and the game folders into one content-addressed map and never
///         dereferences <c>GameFileSource.GameFile</c>, so a correct copy of a game file sitting in the
///         downloads folder satisfies the install. Resolving by path meant this check failed runs the
///         installer would have completed - and made the Steam repair, which puts its files in the
///         downloads folder precisely so the game install is never written to, invisible to the check that
///         asked for it. archive-inventory has already built that map, so this reads it rather than hashing
///         anything again.
///     </para>
///     <para>
///         Which is why it runs after archive-inventory rather than before. At Order 200 it ran ahead of the
///         pruning at 300 and so answered for every game file in the modlist, including ones an install into
///         an existing folder was never going to read: a user updating a list could be stopped over a file
///         nothing was going to touch. nexus-login was moved for the same reason and this follows it. The
///         cost is that a broken game install is now reported one row later, which is the right trade - the
///         row is about work the user actually has left.
///     </para>
///     <para>
///         Game files are never routed to a manual download. A mismatch means the wrong game version, and no
///         browser page will sell you an old build; the way out is fixing the install by hand or, when the
///         game came from a store Wabbajack can fetch from, <see cref="PreflightAction.RepairGameFiles" />.
///     </para>
/// </summary>
public sealed class GameFilesCheck : IPreflightCheck
{
    private const int MaxNamesInMessage = 20;

    public string Id => PreflightCheckIds.GameFiles;
    public string Title => "Game files";
    public int Order => 350;

    public IReadOnlyList<string> DependsOn => new[]
    {
        PreflightCheckIds.GameInstalled, PreflightCheckIds.ArchiveInventory
    };

    public Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var game = ctx.Config.Game;
        var folder = ctx.State.GameFolder;
        var meta = game.MetaData();

        var missingRequired = meta.RequiredFiles
            .Where(r => !folder.Combine(r).FileExists())
            .ToList();
        if (missingRequired.Count > 0)
        {
            return Task.FromResult(PreflightResult.Failed(
                $"The {meta.HumanFriendlyGameName} install at \"{folder}\" is incomplete: " +
                $"{Summarise(missingRequired.Select(r => r.ToString()))}",
                string.Join(Environment.NewLine, missingRequired)));
        }

        var gameFiles = ctx.State.RequiredArchives.Where(a => a.State is GameFileSource).ToArray();
        if (gameFiles.Length == 0)
        {
            ctx.State.RepairableGameFiles = Array.Empty<RepairableGameFile>();
            return Task.FromResult(PreflightResult.Passed("This install takes no files from the game"));
        }

        progress.Report(0, gameFiles.Length);

        var repairable = new List<RepairableGameFile>();
        var verified = 0;
        var done = 0L;

        foreach (var archive in gameFiles)
        {
            token.ThrowIfCancellationRequested();
            var state = (GameFileSource) archive.State;
            var outcome = Classify(ctx, archive, state);
            if (outcome == Outcome.Verified) verified++;
            else repairable.Add(new RepairableGameFile(archive, state,
                outcome == Outcome.Missing ? GameFileProblem.Missing : GameFileProblem.Mismatched));

            progress.Report(++done, gameFiles.Length);
            progress.Archive(archive, outcome switch
            {
                Outcome.Verified => ArchiveState.Present,
                Outcome.Missing => ArchiveState.Missing,
                _ => ArchiveState.Failed
            }, outcome switch
            {
                Outcome.Missing => "Not found in the game folder or the downloads folder",
                Outcome.Mismatch => "Does not match the version this list was built against",
                _ => null
            });
        }

        ctx.State.RepairableGameFiles = repairable;

        if (repairable.Count == 0)
            return Task.FromResult(PreflightResult.Passed($"{Plural.Of(verified, "game file")} verified"));

        var missing = repairable.Where(r => r.Problem == GameFileProblem.Missing).ToList();
        var mismatched = repairable.Where(r => r.Problem == GameFileProblem.Mismatched).ToList();

        var parts = new List<string>();
        var detail = new List<string>();

        if (mismatched.Count > 0)
        {
            var expected = string.Join(", ", mismatched.Select(m => m.Version).Where(v => v != null).Distinct());
            var actual = GameVersionDetector.Detect(game, folder, ctx.GameLocator, ctx.Logger);
            var versions = expected.Length > 0
                ? $"built against {expected}; you have {actual ?? "an unknown version"}"
                : $"you have {actual ?? "an unknown version"}";
            parts.Add(
                $"{Plural.Of(mismatched.Count, "game file doesn't match", "game files don't match")} ({versions}): " +
                Summarise(mismatched.Select(m => m.Archive.Name)));
            detail.Add("Mismatched:");
            detail.AddRange(mismatched.Select(m => "  " + m.Archive.Name));
        }

        if (missing.Count > 0)
        {
            parts.Add(
                $"{Plural.Of(missing.Count, "game file is missing", "game files are missing")} (missing DLC or a modified install?): " +
                Summarise(missing.Select(m => m.Archive.Name)));
            detail.Add("Missing:");
            detail.AddRange(missing.Select(m => "  " + m.Archive.Name));
        }

        var actions = Offer(ctx, repairable, detail);

        return Task.FromResult(PreflightResult.Failed(string.Join(" ", parts),
            string.Join(Environment.NewLine, detail), actions));
    }

    /// <summary>
    ///     Whether the repair is worth putting in front of the user, and what to tell them about it.
    ///     Only when the game actually came from a store the restorer can fetch from: offering to fetch
    ///     Steam depots to somebody whose copy is from GOG is an offer that ends in "you own nothing".
    ///     The offer stands whether or not anyone is logged in - a user who is not has to be told what
    ///     logging in would get them, and then decide. It is never done to them.
    /// </summary>
    private static PreflightAction[]? Offer(PreflightContext ctx, List<RepairableGameFile> repairable,
        List<string> detail)
    {
        var restorer = ctx.GameFileRestorer;
        if (restorer == null) return null;
        if (!repairable.Any(r => ctx.GameLocator.TryGetSteamBuildId(r.State.Game, out _))) return null;

        var status = restorer.Status();
        detail.Add(string.Empty);
        detail.Add(status.Ready
            ? $"{restorer.SourceName} can fetch {Plural.Of(repairable.Count, "file")} " +
              "into your downloads folder, which is all the install needs. Your game install is never " +
              "written to, and the files can be deleted again afterwards."
            : status.Reason);

        return new[] {PreflightAction.RepairGameFiles};
    }

    /// <summary>
    ///     Present when the inventory found these bytes somewhere - the downloads folder counts, and so does
    ///     any of the game folders. Otherwise the file's own path decides which of the two problems it is:
    ///     a file sitting there with other bytes is the wrong game version, and nothing there at all is a
    ///     DLC or a tool that was never installed. That distinction is what tells the repair whether it can
    ///     ask Steam for today's build or has to go to the version index for a historical manifest.
    /// </summary>
    private static Outcome Classify(PreflightContext ctx, Archive archive, GameFileSource state)
    {
        if (ctx.State.HashedArchives.ContainsKey(archive.Name)) return Outcome.Verified;

        AbsolutePath root;
        if (state.Game == ctx.Config.Game)
            root = ctx.State.GameFolder;
        else if (!ctx.State.OtherGameFolders.TryGetValue(state.Game, out root))
            return Outcome.Missing;

        return state.GameFile.RelativeTo(root).FileExists() ? Outcome.Mismatch : Outcome.Missing;
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
