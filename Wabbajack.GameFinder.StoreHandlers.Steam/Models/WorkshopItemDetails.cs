using System;
using System.Diagnostics.CodeAnalysis;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Represents an installed workshop item.
/// </summary>
/// <seealso cref="WorkshopManifest"/>
[PublicAPI]
public sealed record WorkshopItemDetails
{
    /// <summary>
    /// Gets the unique identifier associated with this item.
    /// </summary>
    public required WorkshopItemId ItemId { get; init; }

    /// <summary>
    /// Gets the size of the item on disk.
    /// </summary>
    public required Size SizeOnDisk { get; init; }

    /// <summary>
    /// Gets the unique identifier of the change tracking manifest.
    /// </summary>
    public required WorkshopManifestId ManifestId { get; init; }

    /// <summary>
    /// Gets the time when the item was last updated.
    /// </summary>
    /// <remarks>
    /// If this value is missing, <see cref="DateTimeOffset.UnixEpoch"/>
    /// will be used as the default value.
    /// </remarks>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets the time when the item was last "touched".
    /// </summary>
    /// <remarks>
    /// This value will be set to <see cref="DateTimeOffset.UnixEpoch"/> if the current
    /// workshop item has been downloaded, but not applied yet.
    /// </remarks>
    public DateTimeOffset LastTouched { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets the <see cref="SteamId"/> associated with the account that subscribed to this workshop item.
    /// </summary>
    /// <remarks>
    /// The value saved in the file is a raw 64-bit unsigned integer, but a 32-bit
    /// unsigned integer that only represents the <see cref="SteamId.AccountId"/>.
    /// Due to this, <see cref="SteamId.FromAccountId"/> is used. However, this
    /// implies that the account universe is <see cref="SteamUniverse.Public"/>
    /// and the account type is <see cref="SteamAccountType.Individual"/>.
    /// </remarks>
    /// <example><c>76561193815254978</c></example>
    public SteamId SubscribedBy { get; init; }

    /// <summary>
    /// Gets the URL to the Steam Workshop page of this item.
    /// </summary>
    /// <example><c>https://steamcommunity.com/sharedfiles/filedetails/?id=942405260</c></example>
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public string SteamWorkshopUrl => ItemId.GetSteamWorkshopUrl();
}
