using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Resolves the primary game folder (an explicit <c>Config.GameFolder</c> wins, otherwise the locator)
///     and every <c>OtherGames</c> folder it can, and records them on the blackboard. Never calls the
///     throwing <c>GameLocation</c>.
/// </summary>
public sealed class GameInstalledCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.GameInstalled;
    public string Title => "Game installed";
    public int Order => 200;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var game = ctx.Config.Game;
        var meta = game.MetaData();
        var browse = new[] {PreflightAction.BrowseGameFolder};

        AbsolutePath folder;
        if (ctx.Config.GameFolder != default)
        {
            folder = ctx.Config.GameFolder;
            if (!folder.DirectoryExists())
                return Task.FromResult(PreflightResult.Failed(
                    $"The {meta.HumanFriendlyGameName} folder \"{folder}\" does not exist", actions: browse));
        }
        else if (!ctx.GameLocator.TryFindLocation(game, out folder))
        {
            var otherGame = meta.CommonlyConfusedWith
                .Where(g => ctx.GameLocator.IsInstalled(g)).Select(g => g.MetaData()).FirstOrDefault();
            var message = otherGame != null
                ? $"In order to do a proper install Wabbajack needs to know where your {meta.HumanFriendlyGameName} folder resides. " +
                  $"However this game doesn't seem to be installed, we did however find an installed copy of " +
                  $"{otherGame.HumanFriendlyGameName}, did you install the wrong game?"
                : $"In order to do a proper install Wabbajack needs to know where your {meta.HumanFriendlyGameName} folder resides. " +
                  "However this game doesn't seem to be installed.";
            ctx.Logger.LogError("{Message}", message);
            return Task.FromResult(PreflightResult.Failed(message, actions: browse));
        }
        else if (!folder.DirectoryExists())
        {
            return Task.FromResult(PreflightResult.Failed(
                $"Located {meta.HumanFriendlyGameName} at \"{folder}\" but the folder does not exist", actions: browse));
        }

        ctx.Config.GameFolder = folder;
        ctx.State.GameFolder = folder;

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

        return Task.FromResult(PreflightResult.Passed($"{meta.HumanFriendlyGameName} found at {folder}", detail));
    }
}
