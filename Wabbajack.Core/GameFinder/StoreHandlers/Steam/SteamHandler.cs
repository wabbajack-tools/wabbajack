using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.GameFinder.Common;
using Wabbajack.GameFinder.RegistryUtils;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using Wabbajack.GameFinder.StoreHandlers.Steam.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.GameFinder.Paths;
using OneOf;
using ValveKeyValue;

namespace Wabbajack.GameFinder.StoreHandlers.Steam;

/// <summary>
/// Handler for finding games installed with Steam.
/// </summary>
[PublicAPI]
public class SteamHandler : AHandler<SteamGame, AppId>
{
    private readonly IRegistry? _registry;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    private static readonly KVSerializerOptions KvSerializerOptions =
        new()
        {
            HasEscapeSequences = true,
            EnableValveNullByteBugBehavior = true,
        };

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface.
    /// </param>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. On Linux use <c>null</c>.
    /// For tests either use <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface.
    /// </param>
    /// <param name="logger">Logger.</param>
    public SteamHandler(IFileSystem fileSystem, IRegistry? registry, ILogger<SteamHandler>? logger = null)
    {
        _fileSystem = fileSystem;
        _registry = registry;
        _logger = logger ?? NullLogger<SteamHandler>.Instance;
    }

    /// <inheritdoc/>
    public override Func<SteamGame, AppId> IdSelector => game => game.AppId;

    /// <inheritdoc/>
    public override IEqualityComparer<AppId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override IEnumerable<OneOf<SteamGame, ErrorMessage>> FindAllGames()
    {
        if (!SteamLocationFinder.TryFindSteam(_fileSystem, _registry, _logger, out var steamPath)) yield break;
        var libraryFoldersFilePath = SteamLocationFinder.GetLibraryFoldersFilePath(steamPath);

        var libraryFoldersResult = LibraryFoldersManifestParser.ParseManifestFile(libraryFoldersFilePath);
        if (libraryFoldersResult.IsFailed)
        {
            _logger.LogWarning("Failed to parse library folders file `{FilePath}`: `{Errors}`", libraryFoldersFilePath, libraryFoldersResult.Errors);
            yield break;
        }

        var libraryFolders = libraryFoldersResult.Value;
        _logger.LogInformation("Found `{Count}` library folders in the library folders file `{FilePath}`", libraryFolders.Count, libraryFoldersFilePath);

        foreach (var libraryFolder in libraryFolders)
        {
            var libraryFolderPath = libraryFolder.Path;
            _logger.LogDebug("Testing library folder `{Path}`", libraryFolderPath);

            if (!_fileSystem.DirectoryExists(libraryFolderPath))
            {
                _logger.LogWarning("Steam library folder at `{Path}` doesn't exist", libraryFolderPath);
                continue;
            }

            var appManifestFiles = libraryFolder.EnumerateAppManifestFilePaths().ToArray();
            _logger.LogInformation("Found `{Count}` app manifest files in library folder `{LibraryFolder}`", appManifestFiles.Length, libraryFolderPath);

            foreach (var acfFilePath in appManifestFiles)
            {
                _logger.LogDebug("Testing app manifest file `{Path}`", acfFilePath);

                var appManifestResult = AppManifestParser.ParseManifestFile(acfFilePath);
                if (appManifestResult.IsFailed)
                {
                    _logger.LogWarning("Failed to parse app manifest file `{Path}`: `{Errors}`", acfFilePath, appManifestResult.Errors);
                    continue;
                }

                var steamGame = new SteamGame
                {
                    SteamPath = steamPath,
                    AppManifest = appManifestResult.Value,
                    LibraryFolder = libraryFolder,
                };

                yield return steamGame;
            }
        }
    }
}
