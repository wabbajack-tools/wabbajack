using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths.Utilities;
using Reloaded.Memory.Extensions;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// A path that represents a partial path to a file or directory.
/// </summary>
[PublicAPI]
public readonly struct RelativePath : IPath<RelativePath>, IEquatable<RelativePath>, IComparable<RelativePath>
{
    // NOTE(erri120): since relative paths are not rooted, the operating system
    // shouldn't matter. The OS is usually only relevant to determine the root part
    // of a path.
    // ReSharper disable once InconsistentNaming
    private static readonly IOSInformation OS = OSInformation.Shared;

    /// <summary>
    /// Gets the comparer used for sorting.
    /// </summary>
    public static readonly RelativePathComparer Comparer;

    /// <summary>
    /// Represents an empty path.
    /// </summary>
    public static RelativePath Empty => new(string.Empty);

    /// <summary>
    /// Contains the relative path stored in this instance.
    /// </summary>
    public readonly string Path;

    /// <inheritdoc />
    public Extension Extension => Extension.FromPath(Path);

    /// <summary>
    /// Returns the file name of this path.
    /// </summary>
    public RelativePath FileName => Name;

    /// <inheritdoc />
    public RelativePath Name => new(PathHelpers.GetFileName(Path).ToString());

    /// <summary>
    /// Amount of directories contained within this relative path.
    /// </summary>
    public int Depth => PathHelpers.GetDirectoryDepth(Path);

    /// <summary>
    /// Traverses one directory up.
    /// </summary>
    public RelativePath Parent
    {
        get
        {
            var directoryName = PathHelpers.GetDirectoryName(Path);
            return directoryName.IsEmpty ? Empty : new RelativePath(directoryName.ToString());
        }
    }

    /// <summary>
    /// Always returns an empty path as relative paths are not rooted.
    /// </summary>
    public RelativePath GetRootComponent => RelativePath.Empty;

    /// <summary>
    /// Returns the length of the <see cref="RelativePath"/>.
    /// </summary>
    public int Length => Path.Length;

    /// <inheritdoc/>
    public IEnumerable<RelativePath> Parts => GetParts();

    /// <inheritdoc/>
    public IEnumerable<RelativePath> GetAllParents()
    {
        var parentPath = this;
        while (parentPath != Empty)
        {
            yield return parentPath;
            parentPath = parentPath.Parent;
        }
    }

    /// <summary>
    /// Always returns itself as relative paths are not rooted.
    /// </summary>
    /// <returns></returns>
    public RelativePath GetNonRootPart()
    {
        return this;
    }

    /// <inheritdoc />
    public bool IsRooted => false;

    /// <summary>
    /// Obtains the name of the first folder stored in this path.
    /// </summary>
    /// <remarks>
    /// This will return empty string if there are no child directories.
    /// </remarks>
    public RelativePath TopParent
    {
        get
        {
            var topParent = PathHelpers.GetTopParent(Path);
            return topParent.IsEmpty ? Empty : new RelativePath(topParent.ToString());
        }
    }

    /// <summary>
    /// Creates a relative path given a string.
    /// </summary>
    /// <param name="path">The relative path to use.</param>
    internal RelativePath(string path)
    {
        PathHelpers.DebugAssertIsSanitized(path);
        PathHelpers.AssertIsRooted(path, shouldBeRooted: false);
        Path = path;
    }

    /// <summary>
    /// Creates an unsafe relative path that hasn't been sanitized.
    /// </summary>
    /// <remarks>
    /// This should only be used sparingly if you previously asserted that the path is sanitized.
    /// </remarks>
    public static RelativePath CreateUnsafe(string path)
    {
        return new RelativePath(path);
    }

    /// <summary>
    /// Creates a new <see cref="RelativePath"/> from a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="path"></param>
    public static RelativePath FromUnsanitizedInput(ReadOnlySpan<char> path)
    {
        return new RelativePath(PathHelpers.Sanitize(path));
    }

    /// <summary>
    /// Returns the path with the directory separators native to the passed operating system.
    /// </summary>
    public string ToNativeSeparators(IOSInformation os)
    {
        return PathHelpers.ToNativeSeparators(Path, os);
    }

    /// <summary>
    /// Returns a new path that is this path with the extension changed.
    /// </summary>
    /// <param name="newExtension">The extension to replace the old extension.</param>
    public RelativePath ReplaceExtension(Extension newExtension)
    {
        return new RelativePath(PathHelpers.ReplaceExtension(Path, newExtension.ToString()));
    }

    /// <summary>
    /// Adds an extension to the relative path.
    /// </summary>
    /// <param name="ext">The extension to add.</param>
    public RelativePath WithExtension(Extension ext) => new(Path + ext);

    /// <summary>
    /// Appends another path to an existing path.
    /// </summary>
    /// <param name="other">The path to append.</param>
    /// <returns>Combinations of both paths.</returns>
    [Pure]
    public RelativePath Join(RelativePath other)
    {
        return new RelativePath(PathHelpers.JoinParts(Path, other.Path));
    }
    
    /// <summary>
    /// Appends another path to an existing path.
    /// </summary>
    public static RelativePath operator /(RelativePath self, RelativePath other) => self.Join(other);

    /// <summary>
    /// Returns true if the relative path starts with a given string.
    /// </summary>
    public bool StartsWith(ReadOnlySpan<char> other)
    {
        return Path.AsSpan().StartsWith(other, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Retrieves all of the individual parts that make up the path.
    /// </summary>
    /// <remarks>
    ///     Prefer this over <see cref="Parts"/> as it returns the concrete underlying array type rather than an iterator.
    /// </remarks>
    public RelativePath[] GetParts()
    {
        // Get the path as a ReadOnlySpan<char> for efficient processing
        ReadOnlySpan<char> path = Path;

        // Return an empty array if the path is empty
        if (path.Length == 0)
            return Array.Empty<RelativePath>();

        // Count the number of parts in the path based on the directory separator character
        // ReSharper disable once RedundantNameQualifier
        var partCount = Reloaded.Memory.Extensions.SpanExtensions.Count(path, PathHelpers.DirectorySeparatorChar);

        // Allocate an array to hold the parts
        var parts = GC.AllocateUninitializedArray<RelativePath>(partCount + 1);

        // Variables to keep track of the current position and index within the parts array
        var prev = 0;
        var partIdx = 0;

        int idx; // Current index in the path
        // Loop through the path until no more separators are found
        while ((idx = path.SliceFast(prev).IndexOf(PathHelpers.DirectorySeparatorChar)) != -1)
        {
            parts[partIdx++] = new RelativePath(new string(path.Slice(prev, idx)));

            // Update the start position for the next part
            prev += idx + 1;
        }

        // Handle the last part of the path (after the last separator)
        parts[partIdx] = new RelativePath(new string(path.SliceFast(prev)));

        // Return the array of parts
        return parts;
    }

    /// <inheritdoc/>
    public bool StartsWith(RelativePath other)
    {
        if (other.Path.Length == 0) return true;
        if (other.Path.Length > Path.Length) return false;
        if (other.Path.Length == Path.Length) return Equals(other);
        if (!Path.AsSpan().StartsWith(other.Path, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // If the other path is a parent of this path, then the next character must be a directory separator.
        return Path[other.Path.Length] == PathHelpers.DirectorySeparatorChar;
    }

    /// <summary>
    /// Returns true if the relative path ends with a given string.
    /// </summary>
    public bool EndsWith(ReadOnlySpan<char> other)
    {
        return Path.AsSpan().EndsWith(other, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool EndsWith(RelativePath other)
    {
        if (other.Path.Length == 0) return true;
        if (other.Path.Length > Path.Length) return false;
        if (other.Path.Length == Path.Length) return Equals(other);
        if (!Path.AsSpan().EndsWith(other.Path, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // If this path ends with other but is longer, then the character before the other path must be a directory separator.
        return Path[Path.Length - other.Path.Length - 1] == PathHelpers.DirectorySeparatorChar;
    }

    /// <inheritdoc />
    public bool InFolder(RelativePath other)
    {
        return PathHelpers.InFolder(Path, other.Path);
    }

    /// <summary>
    /// Drops first X directories of a path.
    /// </summary>
    /// <param name="numDirectories">Number of directories to drop.</param>
    public RelativePath DropFirst(int numDirectories = 1)
    {
        var res = PathHelpers.DropParents(Path, numDirectories);
        return res.IsEmpty ? Empty : new RelativePath(res.ToString());
    }

    /// <summary>
    /// Returns a path relative to the sub-path specified.
    /// </summary>
    /// <remarks>
    /// Returns an empty path if <paramref name="basePath"/> matches this path.
    /// </remarks>
    /// <param name="basePath">The sub-path specified.</param>
    /// <throws><see cref="PathException"/> if <paramref name="basePath"/> is not a parent of this path.</throws>
    public RelativePath RelativeTo(RelativePath basePath)
    {
        var other = basePath.Path;
        if (other.Length == 0) return this;
        if (basePath.Path == Path) return Empty;

        var res = PathHelpers.RelativeTo(Path, other);
        if (!res.IsEmpty) return new RelativePath(res.ToString());

        ThrowHelpers.PathException($"Path '{Path}' is not relative to '{other}'");
        return default;
    }

    /// <inheritdoc />
    public override string ToString() => Path;

    #region Equals & GetHashCode

    /// <inheritdoc />
    public bool Equals(RelativePath other) => PathHelpers.PathEquals(Path, other.Path);

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is RelativePath other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return PathHelpers.PathHashCode(Path);
    }

    #endregion

    /// <summary/>
    public static implicit operator string(RelativePath d) => d.Path;

    /// <summary/>
    public static implicit operator ReadOnlySpan<char>(RelativePath d) => d.Path;

    /// <summary/>
    public static implicit operator RelativePath(string path) => FromUnsanitizedInput(path);

    /// <summary/>
    public static bool operator ==(RelativePath lhs, RelativePath rhs) => lhs.Equals(rhs);

    /// <summary/>
    public static bool operator !=(RelativePath lhs, RelativePath rhs) => !(lhs == rhs);

    /// <inheritdoc />
    public int CompareTo(RelativePath other) => PathHelpers.Compare(Path, other.Path);
}

/// <summary>
/// Compares two relative paths for sorting purposes.
/// </summary>
[PublicAPI]
public readonly struct RelativePathComparer : IComparer<RelativePath>
{
    /// <inheritdoc />
    public int Compare(RelativePath x, RelativePath y) => x.CompareTo(y);
}
