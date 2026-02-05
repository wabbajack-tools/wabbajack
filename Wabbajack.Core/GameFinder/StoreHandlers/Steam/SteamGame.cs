using FluentResults;
using Wabbajack.GameFinder.Common;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using Wabbajack.GameFinder.StoreHandlers.Steam.Services;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.Steam;

/// <summary>
/// Represents a game installed with Steam.
/// </summary>
[PublicAPI]
public sealed record SteamGame : IGame
{
    /// <summary>
    /// Gets the parsed <see cref="AppManifest"/> of this game.
    /// </summary>
    public required AppManifest AppManifest { get; init; }

    /// <summary>
    /// Gets the library folder that contains this game.
    /// </summary>
    public required LibraryFolder LibraryFolder { get; init; }

    /// <summary>
    /// Gets the path to the global Steam installation.
    /// </summary>
    public required AbsolutePath SteamPath { get; init; }

    #region Helpers

    /// <inheritdoc cref="Models.AppManifest.AppId"/>
    public AppId AppId => AppManifest.AppId;

    /// <inheritdoc cref="Models.AppManifest.Name"/>
    public string Name => AppManifest.Name;

    /// <summary>
    /// Gets the absolute path to the game's installation directory.
    /// </summary>
    public AbsolutePath Path => AppManifest.InstallationDirectory;

    /// <summary>
    /// Gets the absolute path to the cloud saves directory.
    /// </summary>
    public AbsolutePath GetCloudSavesDirectoryPath() => AppManifest.GetUserDataDirectoryPath(SteamPath);

    /// <summary>
    /// Gets the Wine prefix managed by Proton for this game, if it exists.
    /// </summary>
    public ProtonWinePrefix? GetProtonPrefix()
    {
        var protonDirectory = AppManifest.GetCompatabilityDataDirectoryPath();
        if (!protonDirectory.DirectoryExists()) return null;

        var configurationDirectory = protonDirectory.Combine("pfx");
        return new ProtonWinePrefix
        {
            ConfigurationDirectory = configurationDirectory,
            ProtonDirectory = protonDirectory,
        };
    }

    /// <summary>
    /// Uses <see cref="WorkshopManifestParser"/> to parse the workshop manifest
    /// file at <see cref="Models.AppManifest.GetWorkshopManifestFilePath"/>.
    /// </summary>
    /// <seealso cref="WorkshopManifestParser"/>
    [Pure]
    [System.Diagnostics.Contracts.Pure]
    [MustUseReturnValue]
    public Result<WorkshopManifest> ParseWorkshopManifest()
    {
        var workshopManifestFilePath = AppManifest.GetWorkshopManifestFilePath();
        var result = WorkshopManifestParser.ParseManifestFile(workshopManifestFilePath);
        return result;
    }

    #endregion

    #region Overrides

    /// <inheritdoc/>
    public bool Equals(SteamGame? other) => AppManifest.Equals(other?.AppManifest);

    /// <inheritdoc/>
    public override int GetHashCode() => AppManifest.GetHashCode();

    #endregion
}

