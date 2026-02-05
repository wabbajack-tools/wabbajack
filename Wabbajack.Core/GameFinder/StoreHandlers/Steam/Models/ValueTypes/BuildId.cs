using System.Globalization;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;

/// <summary>
/// Represents a 32-bit unsigned integer unique identifier for a build.
/// </summary>
/// <example><c>9545898</c></example>
[PublicAPI]
[ValueObject<uint>]
public readonly partial struct BuildId : IAugmentWith<DefaultValueAugment>
{
    /// <inheritdoc/>
    public static BuildId DefaultValue { get; } = From(0);

    /// <summary>
    /// Gets the URL to the SteamDB Update Notes for the build associated with this id.
    /// </summary>
    /// <example><c>https://steamdb.info/patchnotes/9545898</c></example>
    public string GetSteamDbUpdateNotesUrl() => $"{Constants.SteamDbBaseUrl}/patchnotes/{Value.ToString(CultureInfo.InvariantCulture)}";
}
