using System.Globalization;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Represents a locally installed depot.
/// </summary>
[PublicAPI]
public record InstalledDepot
{
    /// <summary>
    /// Gets the unique identifier of the depot.
    /// </summary>
    /// <example><c>445700</c></example>
    public required DepotId DepotId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the current manifest of the depot.
    /// </summary>
    /// <example><c>560769545274183569</c></example>
    public required ManifestId ManifestId { get; init; }

    /// <summary>
    /// Gets the size of the depot on disk.
    /// </summary>
    public required Size SizeOnDisk { get; init; } = Size.Zero;

    /// <summary>
    /// Gets the optionally unique identifier of the DLC that is associated with this depot.
    /// </summary>
    /// <remarks>
    /// This value can be <see cref="AppId.DefaultValue"/> if the depot is not associated with a DLC.
    /// </remarks>
    public AppId DLCAppId { get; init; } = AppId.DefaultValue;

    /// <summary>
    /// Gets the URL to the SteamDB page for the depot.
    /// </summary>
    public string GetSteamDbUrl() => $"{Constants.SteamDbBaseUrl}/depot/{DepotId.Value.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Gets the URL to the SteamDB page for the Changeset of the current version of this depot, based on the <see cref="ManifestId"/>.
    /// </summary>
    public string GetSteamDbChangeSetUrl() => ManifestId.GetSteamDbChangesetUrl(DepotId);
}
