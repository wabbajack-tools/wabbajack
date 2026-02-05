using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using Wabbajack.GameFinder.StoreHandlers.Steam.Services;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Represents a parsed workshop manifest file.
/// </summary>
/// <remarks>
/// Workshop manifest files <c>appworkshop_*.acf</c> use Valve's custom
/// KeyValue format.
/// </remarks>
[PublicAPI]
public sealed record WorkshopManifest
{
    /// <summary>
    /// Gets the <see cref="AbsolutePath"/> to the <c>appworkshop_*.acf</c> file
    /// that was parsed to produce this <see cref="WorkshopManifest"/>.
    /// </summary>
    /// <example><c>E:/SteamLibrary/steamapps/workshop/appworkshop_262060.acf</c></example>
    [SuppressMessage("ReSharper", "CommentTypo")]
    public required AbsolutePath ManifestPath { get; init; }

    #region Parsed Values

    /// <summary>
    /// Gets the unique identifier of the app, this manifest is relevant to.
    /// </summary>
    public required AppId AppId { get; init; }

    /// <summary>
    /// Gets the combined size of disk of all installed workshop items.
    /// </summary>
    public Size SizeOnDisk { get; init; } = Size.Zero;

    /// <summary>
    /// Gets whether or not Steam needs to update the workshop items.
    /// </summary>
    public bool NeedsUpdate { get; init; }

    /// <summary>
    /// Gets whether or not Steam needs to download some workshop items.
    /// </summary>
    public bool NeedsDownload { get; init; }

    /// <summary>
    /// Gets the time when the workshop items were last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets the time when the app was last started.
    /// </summary>
    /// <remarks>
    /// This value can be compared to <see cref="LastUpdated"/> to check whether
    /// the latest update of the workshop item has been applied yet.
    /// </remarks>
    public DateTimeOffset LastAppStart { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets all installed workshop items.
    /// </summary>
    public IReadOnlyDictionary<WorkshopItemId, WorkshopItemDetails> InstalledWorkshopItems { get; init; } = ImmutableDictionary<WorkshopItemId, WorkshopItemDetails>.Empty;

    #endregion

    #region Helpers

    /// <summary>
    /// Parses the file at <see cref="ManifestPath"/> again and returns a new
    /// instance of <see cref="AppManifest"/>.
    /// </summary>
    [Pure]
    [System.Diagnostics.Contracts.Pure]
    [MustUseReturnValue]
    public Result<WorkshopManifest> Reload()
    {
        return WorkshopManifestParser.ParseManifestFile(ManifestPath);
    }

    private static readonly RelativePath ContentDirectoryName = "content";
    private static readonly RelativePath DownloadsDirectoryName = "downloads";

    /// <summary>
    /// Gets the absolute path to the content directory.
    /// </summary>
    /// <example><c>E:/SteamLibrary/steamapps/workshop/content/262060</c></example>
    public AbsolutePath GetContentDirectoryPath() => ManifestPath.Parent
        .Combine(ContentDirectoryName)
        .Combine(AppId.Value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Gets the absolute path to the downloads directory.
    /// </summary>
    /// <example><c>E:/SteamLibrary/steamapps/workshop/downloads/262060</c></example>
    public AbsolutePath GetDownloadsDirectoryPath() => ManifestPath.Parent
        .Combine(DownloadsDirectoryName)
        .Combine(AppId.Value.ToString(CultureInfo.InvariantCulture));

    #endregion

    #region Overwrites

    /// <inheritdoc/>
    public bool Equals(WorkshopManifest? other)
    {
        if (other is null) return false;
        if (AppId != other.AppId) return false;
        if (SizeOnDisk != other.SizeOnDisk) return false;
        if (NeedsUpdate != other.NeedsUpdate) return false;
        if (NeedsDownload != other.NeedsDownload) return false;
        if (LastUpdated != other.LastUpdated) return false;
        if (LastAppStart != other.LastAppStart) return false;
        if (!InstalledWorkshopItems.SequenceEqual(other.InstalledWorkshopItems)) return false;
        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(AppId);
        hashCode.Add(SizeOnDisk);
        hashCode.Add(NeedsUpdate);
        hashCode.Add(NeedsDownload);
        hashCode.Add(LastUpdated);
        hashCode.Add(LastAppStart);
        hashCode.Add(InstalledWorkshopItems);
        return hashCode.ToHashCode();
    }

    #endregion
}
