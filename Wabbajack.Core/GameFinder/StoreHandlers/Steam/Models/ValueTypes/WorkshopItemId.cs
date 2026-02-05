using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;

/// <summary>
/// Represents a 64-bit unsigned integer unique identifier of a workshop item.
/// </summary>
/// <remarks>
/// Steam's APIs call this a "published file ID", see https://steamapi.xpaw.me/#IPublishedFileService/GetDetails
/// for reference, but calling this a "file identifier" only leads to confusion. This isn't the identifier
/// for a single file, this is the identifier for a Workshop Item, which can have multiple versions and multiple files.
/// </remarks>
/// <example><c>942405260</c></example>
[PublicAPI]
[ValueObject<ulong>]
public readonly partial struct WorkshopItemId : IAugmentWith<DefaultValueAugment>
{
    /// <inheritdoc/>
    public static WorkshopItemId DefaultValue { get; } = From(0);

    /// <summary>
    /// Gets the URL to the Steam Workshop page of this item.
    /// </summary>
    /// <example><c>https://steamcommunity.com/sharedfiles/filedetails/?id=942405260</c></example>
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public string GetSteamWorkshopUrl() => $"{Constants.SteamCommunityBaseUrl}/sharedfiles/filedetails/?id={Value.ToString(CultureInfo.InvariantCulture)}";
}
