using System.Globalization;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;

/// <summary>
/// Represents a 32-bit unsigned integer unique identifier for a depot.
/// </summary>
/// <example><c>262061</c></example>
[PublicAPI]
[ValueObject<uint>]
public readonly partial struct DepotId : IAugmentWith<DefaultValueAugment>
{
    /// <inheritdoc/>
    public static DepotId DefaultValue { get; } = From(0);

    /// <summary>
    /// Gets the URL to the SteamDB page of this depot.
    /// </summary>
    /// <example><c>https://steamdb.info/depot/262061</c></example>
    public string GetSteamDbUrl() => $"{Constants.SteamDbBaseUrl}/depot/{Value.ToString(CultureInfo.InvariantCulture)}";
}
