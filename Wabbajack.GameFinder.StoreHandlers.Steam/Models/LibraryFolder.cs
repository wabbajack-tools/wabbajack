using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;
using Wabbajack.GameFinder.Paths.Extensions;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Represents a single library folder.
/// </summary>
[PublicAPI]
public sealed record LibraryFolder
{
    /// <summary>
    /// Gets the absolute path to the library folder.
    /// </summary>
    public required AbsolutePath Path { get; init; }

    /// <summary>
    /// Gets the label of the library folder.
    /// </summary>
    /// <remarks>This value can be <see cref="string.Empty"/>.</remarks>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total size of the disk that contains this library.
    /// </summary>
    /// <remarks>
    /// Since you usually only have one library per disk (eg: <c>C:/SteamLibrary</c>,
    /// <c>E:/SteamLibrary</c> and <c>M:/SteamLibrary</c>), this value can give you
    /// an idea of how big the drive is. Do note that this value can also be
    /// <see cref="Size.Zero"/> in some situations, for example, on Linux
    /// if Steam doesn't fully understand the file system.
    /// </remarks>
    public Size TotalDiskSize { get; init; } = Size.Zero;

    /// <summary>
    /// Gets all installed apps inside the library folders and their sizes.
    /// </summary>
    /// <seealso cref="GetTotalSizeOfInstalledApps"/>
    public IReadOnlyDictionary<AppId, Size> AppSizes { get; init; } = ImmutableDictionary<AppId, Size>.Empty;

    #region Helpers

    /// <summary>
    /// Calculates the total size of all installed apps.
    /// </summary>
    /// <seealso cref="AppSizes"/>
    public Size GetTotalSizeOfInstalledApps() => AppSizes.Values.Sum();

    /// <summary>
    /// Calculates a free space estimate on the disk using <see cref="TotalDiskSize"/> and <see cref="GetTotalSizeOfInstalledApps"/>.
    /// </summary>
    public Size GetFreeSpaceEstimate() => TotalDiskSize - GetTotalSizeOfInstalledApps();

    public static readonly RelativePath SteamAppsDirectoryName = "steamapps";

    /// <summary>
    /// Returns an enumerable for every <c>appmanifest_*.acf</c> file path in the current library.
    /// </summary>
    public IEnumerable<AbsolutePath> EnumerateAppManifestFilePaths()
    {
        var steamAppsDirectory = Path.Combine(SteamAppsDirectoryName);
        if (!steamAppsDirectory.DirectoryExists()) return Enumerable.Empty<AbsolutePath>();
        return steamAppsDirectory.EnumerateFiles("*.acf", recursive: false);
    }

    #endregion

    #region Overrides

    /// <inheritdoc/>
    public bool Equals(LibraryFolder? other)
    {
        if (other is null) return false;
        if (!Path.Equals(other.Path)) return false;
        if (!Label.Equals(other.Label, StringComparison.Ordinal)) return false;
        if (!TotalDiskSize.Equals(other.TotalDiskSize)) return false;
        if (!AppSizes.SequenceEqual(other.AppSizes)) return false;
        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Path);
        hashCode.Add(Label);
        hashCode.Add(TotalDiskSize);
        hashCode.Add(AppSizes);
        return hashCode.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => Path.GetFullPath();

    #endregion
}
