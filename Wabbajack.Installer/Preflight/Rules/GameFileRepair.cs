using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>Why a game file needs repairing, which decides where the good copy has to come from.</summary>
public enum GameFileProblem
{
    /// <summary>
    ///     Nothing on disk has these bytes and the game folder has no file at that path at all: a DLC that
    ///     was never installed, a tool that was never bought, something that was deleted. The file the list
    ///     wants is very often the one the game publishes today, so the current build is worth asking for
    ///     first and costs no index lookup.
    /// </summary>
    Missing,

    /// <summary>
    ///     The file is there and is not the one the list was built against, which almost always means Steam
    ///     has moved the game past the version the list targets. Today's build is by definition not what is
    ///     wanted, so this one starts from the version index.
    /// </summary>
    Mismatched
}

/// <summary>One game file this install needs and does not have, and what is wrong with it.</summary>
public sealed record RepairableGameFile(Archive Archive, GameFileSource State, GameFileProblem Problem)
{
    /// <summary>The version of the game the modlist recorded this file at, or null when it recorded none.</summary>
    public string? Version => string.IsNullOrWhiteSpace(State.GameVersion) ? null : State.GameVersion;
}

/// <summary>How one repair ended.</summary>
public enum GameFileRepairStatus
{
    /// <summary>Fetched, hashed to what the modlist expects, and placed in the downloads folder.</summary>
    Repaired,

    /// <summary>Nothing was tried: there is no restorer, or nobody has logged into one.</summary>
    NotAttempted,

    /// <summary>Tried and did not produce the file. <c>Message</c> says why, in the restorer's own words.</summary>
    Failed,

    /// <summary>
    ///     Fetched, and the bytes are not the ones this install needs. The file is thrown away rather than
    ///     placed: a wrong game file that hashes to nothing the list asked for would fail the install later
    ///     and look like a corrupt download.
    /// </summary>
    WrongContent
}

/// <param name="Version">The version it was resolved at, for the report. Null means the current build.</param>
/// <param name="Placed">Where it was written, when it was.</param>
public sealed record GameFileRepairResult(Archive Archive, GameFileRepairStatus Status, string Message,
    string? Version = null, AbsolutePath Placed = default)
{
    public string VersionDescription => Version ?? "the current build";
}

/// <summary>
///     Fetches the game files an install needs and the user's game install does not have, and puts them in
///     the downloads folder.
///     <para>
///         The downloads folder, and never the game folder. The installer resolves game files by hash:
///         <c>AInstaller.HashArchives</c> flattens the downloads folder and the game folders into one
///         content-addressed map and asks whether each archive's hash is in it, never dereferencing
///         <c>GameFileSource.GameFile</c>. So a correct-hash copy under downloads satisfies the install with
///         no byte written to a Steam-managed folder: no elevation, nothing for Steam to re-patch, the
///         user's game still playable, and the whole thing undone by deleting a file. Repairing the game
///         install itself is deliberately not something this does.
///     </para>
///     <para>
///         Two hashes are checked and both matter. The restorer checks what it fetched against the hash the
///         source itself carries, which proves the depot handed over what it meant to. This then checks it
///         against <c>Archive.Hash</c>, the modlist's own xxHash64, which is the only thing that proves it
///         is the file <em>this install</em> needs - the same path at a different game version is a
///         perfectly valid file and the wrong answer. A file that fails the second check is deleted.
///     </para>
/// </summary>
public static class GameFileRepair
{
    /// <summary>
    ///     Repairs each file in turn, newest problem first, reporting as it goes.
    ///     Deliberately one at a time rather than through <c>PMapAll</c>: every file in a group comes out of
    ///     the same depot manifest, the restorer remembers manifests it has read, and running wide would
    ///     have several callers fetch the same manifest before any of them had finished remembering it.
    /// </summary>
    /// <param name="items">
    ///     What to repair. Grouped by game and version before anything is fetched, so files sharing a build
    ///     are asked for together and the manifests behind them are read once.
    /// </param>
    public static async Task<IReadOnlyList<GameFileRepairResult>> Run(PreflightContext ctx,
        IEnumerable<RepairableGameFile> items, IPreflightProgress progress, CancellationToken token)
    {
        var work = Group(items);
        var results = new List<GameFileRepairResult>();

        var restorer = ctx.GameFileRestorer;
        if (restorer == null)
        {
            return work
                .Select(i => new GameFileRepairResult(i.Archive, GameFileRepairStatus.NotAttempted,
                    "This build of Wabbajack has no way to fetch game files."))
                .ToList();
        }

        var status = restorer.Status();
        if (!status.Ready)
        {
            return work
                .Select(i => new GameFileRepairResult(i.Archive, GameFileRepairStatus.NotAttempted, status.Reason))
                .ToList();
        }

        ctx.Config.Downloads.CreateDirectory();
        var done = 0;
        progress.Report(0, work.Count, $"Fetching game files from {restorer.SourceName}");

        foreach (var item in work)
        {
            token.ThrowIfCancellationRequested();
            progress.Report(done, work.Count, $"Fetching {item.Archive.Name}");
            progress.Archive(item.Archive, ArchiveState.Downloading, $"Fetching from {restorer.SourceName}");

            var result = await One(ctx, restorer, item, token);
            results.Add(result);
            progress.Archive(item.Archive, ArchiveStatusOf(result.Status), result.Message);
            progress.Report(++done, work.Count, $"Fetching game files from {restorer.SourceName}");
        }

        return results;
    }

    /// <summary>
    ///     By game and then version, so every file out of one build is asked for together. One modlist can
    ///     span versions - each <c>GameFileSource</c> carries its own - and a list that takes files from a
    ///     second game carries that game's version too.
    /// </summary>
    public static IReadOnlyList<RepairableGameFile> Group(IEnumerable<RepairableGameFile> items)
    {
        return items
            .OrderBy(i => i.State.Game)
            .ThenBy(i => i.Version ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Archive.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<GameFileRepairResult> One(PreflightContext ctx, IGameFileRestorer restorer,
        RepairableGameFile item, CancellationToken token)
    {
        // Missing asks the game what it publishes now, which needs no index lookup at all and is usually
        // right - the user never installed the thing, and the thing has not changed. Mismatched already
        // knows today's build is not what is wanted, so it starts from the version the list recorded.
        // Either way the other question is asked second, because a user can be missing a file *and* be on a
        // build the list was not made for, and one question alone would leave that file unrepaired. A list
        // that recorded no version collapses to the one question there is.
        var attempts = (item.Problem == GameFileProblem.Missing
                ? new[] {null, item.Version}
                : new string?[] {item.Version, null})
            .Distinct()
            .ToArray();

        GameFileRepairResult? first = null;

        foreach (var version in attempts)
        {
            token.ThrowIfCancellationRequested();
            var attempt = await Attempt(ctx, restorer, item, version, token);
            if (attempt.Status == GameFileRepairStatus.Repaired) return attempt;

            // Nothing is going to come of a restorer that is not set up, and saying so twice is worse than
            // saying it once.
            if (attempt.Status == GameFileRepairStatus.NotAttempted) return attempt;

            // The first answer is the one worth reporting: it came from the question this file's problem
            // said to ask, and the fallback failing as well tells the user nothing more to act on.
            first ??= attempt;
        }

        return first!;
    }

    private static async Task<GameFileRepairResult> Attempt(PreflightContext ctx, IGameFileRestorer restorer,
        RepairableGameFile item, string? version, CancellationToken token)
    {
        var name = item.Archive.Name;
        var destination = ctx.Config.Downloads.Combine(name);

        // Written beside the destination and moved in only once it hashes, the way every other placement in
        // preflight works: a cancelled or rejected fetch must not leave something in the downloads folder
        // that looks like the archive and is not.
        var incoming = destination.WithExtension(Ext.WjIncoming);

        try
        {
            var fetched = await restorer.Restore(item.State.Game, version, item.State.GameFile, incoming, token);

            if (!fetched.Fetched)
            {
                return new GameFileRepairResult(item.Archive, StatusOf(fetched.Outcome),
                    fetched.Detail ?? $"{restorer.SourceName} could not supply {name}.", version);
            }

            var hash = await ctx.HashCache.FileHashCachedAsync(incoming, token);
            if (hash != item.Archive.Hash)
            {
                return new GameFileRepairResult(item.Archive, GameFileRepairStatus.WrongContent,
                    $"{name} was fetched from {restorer.SourceName} at " +
                    $"{fetched.Version ?? "the current build"} and hashes to {hash}, not the {item.Archive.Hash} " +
                    "this list was built against. It has not been kept.",
                    fetched.Version ?? version);
            }

            await incoming.MoveToAsync(destination, true, token);
            await ctx.HashCache.FileHashWriteCache(destination, hash);
            await destination.WithExtension(Ext.Meta)
                .WriteAllTextAsync(ctx.Dispatcher.MetaIniSection(item.Archive), token);

            // What archive-inventory would have recorded had the file been there when it ran, so a re-run of
            // game-files - or the installer itself - sees it without another pass over the downloads folder.
            ctx.State.HashedArchives[name] = destination;

            ctx.Logger.LogInformation("Fetched {Name} from {Source} ({Detail}) into {Destination}", name,
                restorer.SourceName, fetched.Detail, destination);

            return new GameFileRepairResult(item.Archive, GameFileRepairStatus.Repaired,
                $"Fetched from {restorer.SourceName} and placed in the downloads folder.",
                fetched.Version ?? version, destination);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ctx.Logger.LogError(ex, "Repairing game file {Name} failed", name);
            return new GameFileRepairResult(item.Archive, GameFileRepairStatus.Failed, ex.Message, version);
        }
        finally
        {
            if (incoming.FileExists())
            {
                try
                {
                    incoming.Delete();
                }
                catch (Exception ex)
                {
                    ctx.Logger.LogWarning(ex, "Could not clean up {Path}", incoming);
                }
            }
        }
    }

    private static GameFileRepairStatus StatusOf(GameFileRestoreOutcome outcome)
    {
        return outcome switch
        {
            GameFileRestoreOutcome.NotReady => GameFileRepairStatus.NotAttempted,
            GameFileRestoreOutcome.NoSource => GameFileRepairStatus.NotAttempted,
            _ => GameFileRepairStatus.Failed
        };
    }

    private static ArchiveState ArchiveStatusOf(GameFileRepairStatus status)
    {
        return status switch
        {
            GameFileRepairStatus.Repaired => ArchiveState.Downloaded,
            GameFileRepairStatus.NotAttempted => ArchiveState.Missing,
            _ => ArchiveState.Failed
        };
    }
}
