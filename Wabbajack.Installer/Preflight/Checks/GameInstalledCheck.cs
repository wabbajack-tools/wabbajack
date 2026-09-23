using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Resolves the primary game folder (an explicit <c>Config.GameFolder</c> wins, otherwise the locator)
///     and every <c>OtherGames</c> folder it can, and records them on the blackboard. Never calls the
///     throwing <c>GameLocation</c>.
///     <para>
///         A game that cannot be found is not automatically the end of the run. What the install actually
///         needs from the game is its files, and <c>AInstaller.HashArchives</c> looks for those by hash
///         across the downloads folder as well as the game folders - which is why the repair puts fetched
///         game files in downloads and never touches the game install. So when the game is not here but a
///         source says it can hand those files over, this reports a Warning and the checklist carries on:
///         game-files will list what is missing and offer to fetch it. The user still has to acknowledge
///         the Warning, because installing a list for a game that is not on the machine is a decision, not
///         an oversight.
///     </para>
///     <para>
///         <see cref="IGameFileRestorer.CanSourceGame" /> is the whole of that question and deliberately so:
///         whether the account owns the game is something only the store can answer, and this project is
///         kept clear of any store's client library. A host that registers no source, or a user who is not
///         logged into one, gets exactly the failure they have always had.
///     </para>
/// </summary>
public sealed class GameInstalledCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.GameInstalled;
    public string Title => "Game installed";
    public int Order => 100;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress,
        CancellationToken token)
    {
        var game = ctx.Config.Game;
        var meta = game.MetaData();
        var browse = new[] {PreflightAction.BrowseGameFolder};

        // What a Warning needs to be got past. ContinueAnyway is what a host maps to Acknowledge, and a
        // Warning that offers none can never be accepted - the run would sit at not-ready for ever - while
        // Locate game is still the right answer for somebody whose install is simply somewhere the locator
        // does not look.
        var acceptOrLocate = new[] {PreflightAction.ContinueAnyway, PreflightAction.BrowseGameFolder};

        // Owned here and cleared on every run, like game-files' RepairableGameFiles: a run that decided the
        // game could be sourced, followed by one that found the folder, must not leave the first answer
        // standing for a game that is now installed.
        ctx.State.SourcedGames.Clear();

        AbsolutePath folder = default;
        string? notFound = null;

        if (ctx.Config.GameFolder != default)
        {
            folder = ctx.Config.GameFolder;

            // A folder the user pointed at and that is not there is a mistake to correct rather than a game
            // to fetch. Nothing is sourced over the top of it: they said where the game is.
            if (!folder.DirectoryExists())
                return PreflightResult.Failed(
                    $"The {meta.HumanFriendlyGameName} folder \"{folder}\" does not exist", actions: browse);
        }
        else if (!ctx.GameLocator.TryFindLocation(game, out folder))
        {
            var otherGame = meta.CommonlyConfusedWith
                .Where(g => ctx.GameLocator.IsInstalled(g)).Select(g => g.MetaData()).FirstOrDefault();
            notFound = otherGame != null
                ? $"In order to do a proper install Wabbajack needs to know where your {meta.HumanFriendlyGameName} folder resides. " +
                  $"However this game doesn't seem to be installed, we did however find an installed copy of " +
                  $"{otherGame.HumanFriendlyGameName}, did you install the wrong game?"
                : $"In order to do a proper install Wabbajack needs to know where your {meta.HumanFriendlyGameName} folder resides. " +
                  "However this game doesn't seem to be installed.";
            folder = default;
        }
        else if (!folder.DirectoryExists())
        {
            notFound = $"Located {meta.HumanFriendlyGameName} at \"{folder}\" but the folder does not exist";
            folder = default;
        }

        GameSourceResult? source = null;
        if (notFound != null)
        {
            source = await CanSource(ctx, game, token);
            if (!source.Available)
            {
                ctx.Logger.LogError("{Message}", notFound);
                return PreflightResult.Failed(notFound, Advice(source), browse);
            }
        }

        ctx.Config.GameFolder = folder;
        ctx.State.GameFolder = folder;
        if (folder == default) ctx.State.SourcedGames.Add(game);

        ctx.State.OtherGameFolders.Clear();
        var missingOthers = new List<Game>();
        foreach (var other in ctx.Config.OtherGames ?? Array.Empty<Game>())
        {
            if (ctx.GameLocator.TryFindLocation(other, out var otherFolder) && otherFolder.DirectoryExists())
                ctx.State.OtherGameFolders[other] = otherFolder;
            else
                missingOthers.Add(other);
        }

        var detail = missingOthers.Count == 0
            ? null
            : "Not installed, files will not be sourced from: " +
              string.Join(", ", missingOthers.Select(g => g.MetaData().HumanFriendlyGameName));

        if (folder != default)
            return PreflightResult.Passed($"{meta.HumanFriendlyGameName} found at {folder}", detail);

        ctx.Logger.LogInformation("{Game} is not installed; its files will be fetched from {Source}",
            meta.HumanFriendlyGameName, ctx.GameFileRestorer!.SourceName);

        return PreflightResult.Warning(
            $"{meta.HumanFriendlyGameName} is not installed on this machine, so the files this list takes " +
            $"from it will be fetched from {ctx.GameFileRestorer.SourceName}",
            Join(source!.Reason,
                "The modlist itself installs as usual. Nothing is written to a game folder, and anything " +
                "that points at one - a launcher, MO2's game path - has nowhere to point until you install " +
                "the game.",
                detail),
            acceptOrLocate);
    }

    /// <summary>
    ///     Whether the missing game could be fetched rather than installed. Only ever asked when the folder
    ///     is not there, so an install that has its game pays nothing for this - no login, no round trip to
    ///     a store.
    /// </summary>
    private static async Task<GameSourceResult> CanSource(PreflightContext ctx, Game game, CancellationToken token)
    {
        if (ctx.GameFileRestorer == null)
            return new GameSourceResult(GameSourceOutcome.NoSource,
                "This copy of Wabbajack has no way to fetch game files.");

        try
        {
            return await ctx.GameFileRestorer.CanSourceGame(game, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Asking talks to a store over the network. A source that falls over answers the question it
            // was asked - no, not right now - rather than taking the checklist down with it.
            ctx.Logger.LogWarning(ex, "Could not ask whether {Game} could be fetched instead of installed", game);
            return new GameSourceResult(GameSourceOutcome.Unconfirmed, ex.Message);
        }
    }

    /// <summary>
    ///     What to put under a missing game, when there is something to say. A source that does not carry
    ///     this game at all says nothing: "Steam does not sell this" under "this game is not installed"
    ///     reads as a second failure rather than as the non-answer it is.
    /// </summary>
    private static string? Advice(GameSourceResult source)
    {
        return source.Outcome == GameSourceOutcome.NoSource ? null : source.Reason;
    }

    private static string? Join(params string?[] parts)
    {
        var said = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        return said.Length == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, said);
    }
}
