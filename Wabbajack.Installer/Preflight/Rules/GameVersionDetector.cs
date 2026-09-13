using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     Reads the installed game's version the same way the hash-game-files verb does when it records a
///     version, so a mismatch can be reported as "built against X; you have Y". Returns null when the game
///     has no main executable or the version cannot be read.
/// </summary>
public static class GameVersionDetector
{
    public static string? Detect(Game game, AbsolutePath gameFolder, IGameLocator locator, ILogger? logger = null)
    {
        var gameMeta = game.MetaData();
        if (gameMeta.MainExecutable == null) return null;

        try
        {
            var mainExe = gameFolder.Combine(gameMeta.MainExecutable.Value);
            var info = FileVersionInfo.GetVersionInfo(mainExe.ToString());

            var productVersion = info.ProductVersion?.Trim();
            var fileVersion = info.FileVersion?.Trim();

            // Some Unreal Engine games expose the engine changelist as ProductVersion
            // instead of an actual game version (for example "UE5-CL-0").
            var isUnrealEngineVersion =
                !string.IsNullOrWhiteSpace(productVersion) &&
                productVersion.StartsWith("UE", StringComparison.OrdinalIgnoreCase) &&
                productVersion.Contains("-CL-", StringComparison.OrdinalIgnoreCase);

            if (isUnrealEngineVersion &&
                locator.TryGetSteamBuildId(game, out var steamBuildId))
            {
                return steamBuildId;
            }

            return productVersion ?? fileVersion;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not determine version for {Game}", game);
            return null;
        }
    }
}
