using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using FluentResults;
using Wabbajack.GameFinder.RegistryUtils;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

/// <summary>
/// Finds the current Steam installation.
/// </summary>
[PublicAPI]
public static class SteamLocationFinder
{
    /// <summary>
    /// The name of the <c>config</c> directory.
    /// </summary>
    /// <seealso cref="GetLibraryFoldersFilePath"/>
    public static readonly RelativePath ConfigDirectoryName = "config";

    /// <summary>
    /// The name of the <c>libraryfolders.vdf</c> file.
    /// </summary>
    /// <seealso cref="GetLibraryFoldersFilePath"/>
    public static readonly RelativePath LibraryFoldersFileName = "libraryfolders.vdf";

    /// <summary>
    /// The name of the <c>userdata</c> directory.
    /// </summary>
    /// <seealso cref="GetUserDataDirectoryPath"/>
    public static readonly RelativePath UserDataDirectoryName = "userdata";

    /// <summary>
    /// The registry key used to find Steam.
    /// </summary>
    /// <seealso cref="GetSteamPathFromRegistry"/>
    public const string SteamRegistryKey = @"Software\Valve\Steam";

    /// <summary>
    /// The registry value name to find Steam.
    /// </summary>
    /// <seealso cref="GetSteamPathFromRegistry"/>
    public const string SteamRegistryValueName = "SteamPath";

    /// <inheritdoc cref="TryFindSteam"/>
    [Obsolete($"Use {nameof(TryFindSteam)} instead")]
    public static Result<AbsolutePath> FindSteam(IFileSystem fileSystem, IRegistry? registry, ILogger? logger = null)
    {
        if (TryFindSteam(fileSystem, registry, logger ?? NullLogger.Instance, out var steamPath))
        {
            return steamPath;
        }

        return Result.Fail("Failed to find Steam");
    }

    /// <summary>
    /// Tries to find the Steam installation.
    /// </summary>
    public static bool TryFindSteam(IFileSystem fileSystem, IRegistry? registry, ILogger logger, out AbsolutePath steamPath)
    {
        steamPath = default;

        // 1) try the default installation paths
        var defaultSteamInstallationPath = GetDefaultSteamInstallationPaths(fileSystem)
            .FirstOrDefault(path => IsValidSteamInstallation(path, logger));

        if (defaultSteamInstallationPath != default)
        {
            logger.LogInformation("Found Steam at the default installation path `{Path}`", defaultSteamInstallationPath);
            steamPath = defaultSteamInstallationPath;
            return true;
        }

        // 2) try the registry, if there is any
        if (registry is null)
        {
            logger.LogWarning("Unable to find Steam at the default installation paths");
            return false;
        }

        if (!TryGetSteamPathFromRegistry(fileSystem, registry, logger, out var pathFromRegistry))
        {
            logger.LogWarning("Unable to find Steam at the default installation paths and the registry");
            return false;
        }

        if (!IsValidSteamInstallation(pathFromRegistry, logger)) return false;

        steamPath = pathFromRegistry;
        return true;
    }

    /// <summary>
    /// Checks whether the given Steam installation path is valid.
    /// </summary>
    /// <remarks>
    /// A valid Steam installation requires a existing directory,
    /// and a existing <c>libraryfolders.vdf</c> file. This method
    /// uses <see cref="GetLibraryFoldersFilePath"/> to get that file path.
    /// </remarks>
    public static bool IsValidSteamInstallation(AbsolutePath steamPath, ILogger logger)
    {
        if (!steamPath.DirectoryExists())
        {
            logger.LogDebug("Directory at `{Path}` isn't a valid steam installation because the directory doesn't exist", steamPath);
            return false;
        }

        var libraryFoldersFile = GetLibraryFoldersFilePath(steamPath);
        if (libraryFoldersFile.FileExists) return true;

        logger.LogDebug("Directory at `{DirectoryPath}` isn't a valid steam installation because the library folders file at `{FilePath}` doesn't exist", steamPath, libraryFoldersFile);
        return false;
    }

    /// <summary>
    /// Returns the path to the <c>libraryfolders.vdf</c> file inside the Steam <c>config</c>
    /// directory.
    /// </summary>
    public static AbsolutePath GetLibraryFoldersFilePath(AbsolutePath steamPath)
    {
        return steamPath
            .Combine(ConfigDirectoryName)
            .Combine(LibraryFoldersFileName);
    }

    /// <summary>
    /// Returns the path to the user data directory of the provided user.
    /// </summary>
    public static AbsolutePath GetUserDataDirectoryPath(AbsolutePath steamPath, SteamId steamId)
    {
        return steamPath
            .Combine(UserDataDirectoryName)
            .Combine(steamId.AccountId.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc cref="TryGetSteamPathFromRegistry"/>
    [Obsolete($"Use {nameof(TryGetSteamPathFromRegistry)} instead")]
    public static Result<AbsolutePath> GetSteamPathFromRegistry(IFileSystem fileSystem, IRegistry registry, ILogger? logger = null)
    {
        if (TryGetSteamPathFromRegistry(fileSystem, registry, logger ?? NullLogger.Instance, out var steamPath))
        {
            return steamPath;
        }

        return Result.Fail("Unable to get Steam path from registry");
    }

    /// <summary>
    /// Tries to get the Steam installation path from the registry.
    /// </summary>
    public static bool TryGetSteamPathFromRegistry(
        IFileSystem fileSystem,
        IRegistry registry,
        ILogger logger,
        out AbsolutePath steamPath)
    {
        steamPath = default;

        try
        {
            var currentUser = registry.OpenBaseKey(RegistryHive.CurrentUser);

            using var regKey = currentUser.OpenSubKey(SteamRegistryKey);
            if (regKey is null)
            {
                logger.LogWarning("Unable to open the Steam registry key `{RegistryKey}`", SteamRegistryKey);
                return false;
            }

            if (!regKey.TryGetString(SteamRegistryValueName, out var steamPathString))
            {
                logger.LogWarning("Unable to get string value `{RegistryValueName}` from Steam registry key `{RegistryKey}`", SteamRegistryValueName, SteamRegistryKey);
                return false;
            }

            steamPath = fileSystem.FromUnsanitizedFullPath(steamPathString);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception thrown while getting the Steam installation path from the registry");
            return false;
        }
    }

    /// <summary>
    /// Returns all possible default Steam installation paths for the given platform.
    /// </summary>
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    [SuppressMessage("ReSharper", "CommentTypo")]
    public static IEnumerable<AbsolutePath> GetDefaultSteamInstallationPaths(IFileSystem fileSystem)
    {
        if (fileSystem.OS.IsWindows)
        {
            yield return fileSystem
                .GetKnownPath(KnownPath.ProgramFilesX86Directory)
                .Combine("Steam");

            yield break;
        }

        if (fileSystem.OS.IsLinux)
        {
            // "$XDG_DATA_HOME/Steam" which is usually "~/.local/share/Steam"
            yield return fileSystem
                .GetKnownPath(KnownPath.LocalApplicationDataDirectory)
                .Combine("Steam");

            // "~/.steam/debian-installation"
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .Combine(".steam")
                .Combine("debian-installation");

            // "~/.var/app/com.valvesoftware.Steam/data/Steam" (flatpak installation)
            // see https://github.com/flatpak/flatpak/wiki/Filesystem for details
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .Combine(".var/app/com.valvesoftware.Steam/data/Steam");

            // "~/.var/app/com.valvesoftware.Steam/.local/share/Steam" (flatpak installation)
            // see https://github.com/flatpak/flatpak/wiki/Filesystem for details
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .Combine(".var/app/com.valvesoftware.Steam/.local/share/Steam");

            // "~/snap/steam/common/.local/share/Steam" (snap installation)
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .Combine("snap/steam/common/.local/share/Steam");

            // "~/.steam/steam"
            // this is a legacy installation directory and is often soft linked to
            // the actual installation directory
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .Combine(".steam")
                .Combine("steam");

            // "~/.steam"
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .Combine(".steam");

            // "~/.local/.steam"
            yield return fileSystem.GetKnownPath(KnownPath.HomeDirectory)
                .Combine(".local")
                .Combine(".steam");

            yield break;
        }

        if (fileSystem.OS.IsOSX)
        {
            // ~/Library/Application Support/Steam
            yield return fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
                .Combine("Steam");

            yield break;
        }

        throw new PlatformNotSupportedException("GameFinder doesn't support the current platform!");
    }
}
