using JetBrains.Annotations;
using TransparentValueObjects;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;

/// <summary>
/// Represents a unique identifier for a manifest of a workshop item change.
/// </summary>
/// <remarks>
/// Not to be confused with <see cref="ManifestId"/>.
/// </remarks>
/// <example><c>1625768140039815850</c></example>
/// <seealso cref="ManifestId"/>
[PublicAPI]
[ValueObject<ulong>]
public readonly partial struct WorkshopManifestId : IAugmentWith<DefaultValueAugment>
{
    /// <inheritdoc/>
    public static WorkshopManifestId DefaultValue { get; } = From(0);
}
