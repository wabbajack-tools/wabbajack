using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;

/// <summary>
/// Represents a unique identifier for a manifest of a depot change.
/// </summary>
/// <remarks>
/// I've seen conflicting info about this data type online. On one hand, you
/// have some random comment that suggests these values are actually
/// just strings (https://github.com/SteamDatabase/ValveKeyValue/pull/47#issuecomment-984605893),
/// and on another you have the Steam API, which uses <c>uint64</c> for manifest IDs
/// (https://steamapi.xpaw.me/#IContentServerDirectoryService/GetDepotPatchInfo).
/// </remarks>
/// <example><c>5542773349944116172</c></example>
[PublicAPI]
[ValueObject<ulong>]
public readonly partial struct ManifestId : IAugmentWith<DefaultValueAugment>
{
    /// <inheritdoc/>
    public static ManifestId DefaultValue { get; } = From(0);

    /// <summary>
    /// Gets the URL to the SteamDB page for the Changeset of the manifest associated with this id.
    /// </summary>
    /// <param name="depotId">ID of the depot this manifest ID is a part of.</param>
    /// <returns></returns>
    /// <example><c>https://steamdb.info/depot/262061/history/?changeid=M:5542773349944116172</c></example>
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public string GetSteamDbChangesetUrl(DepotId depotId)
    {
        return $"{depotId.GetSteamDbUrl()}/history/?changeid=M:{Value}";
    }
}
