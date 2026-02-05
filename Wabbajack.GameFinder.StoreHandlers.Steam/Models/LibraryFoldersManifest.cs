using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Services;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Represents a parsed <c>libraryfolders.vdf</c> file.
/// </summary>
[PublicAPI]
public sealed record LibraryFoldersManifest : IReadOnlyList<LibraryFolder>
{
    /// <summary>
    /// Gets the absolute path to the parsed manifest file.
    /// </summary>
    /// <example><c>/home/gabe_newell/.local/share/Steam/config/libraryfolders.vdf</c></example>
    public required AbsolutePath ManifestPath { get; init; }

    /// <summary>
    /// Gets all library folders.
    /// </summary>
    public required IReadOnlyList<LibraryFolder> LibraryFolders { get; init; }

    /// <summary>
    /// Parses the file at <see cref="ManifestPath"/> again and returns a new
    /// instance of <see cref="LibraryFoldersManifest"/>.
    /// </summary>
    [Pure]
    [System.Diagnostics.Contracts.Pure]
    [MustUseReturnValue]
    public Result<LibraryFoldersManifest> Reload()
    {
        return LibraryFoldersManifestParser.ParseManifestFile(ManifestPath);
    }

    #region Overrides

    /// <inheritdoc/>
    public bool Equals(LibraryFoldersManifest? other)
    {
        if (other is null) return false;
        if (!LibraryFolders.SequenceEqual(other.LibraryFolders)) return false;
        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(LibraryFolders);
        return hashCode.ToHashCode();
    }

    /// <inheritdoc/>
    public IEnumerator<LibraryFolder> GetEnumerator() => LibraryFolders.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public int Count => LibraryFolders.Count;

    /// <inheritdoc/>
    public LibraryFolder this[int index] => LibraryFolders[index];

    #endregion
}
