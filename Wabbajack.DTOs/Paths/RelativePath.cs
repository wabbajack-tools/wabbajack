using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Wabbajack.Paths;

/// <summary>
/// A relative path that uses backslashes for Windows modlist compatibility.
/// Paths are stored as strings internally with backslash separators.
/// </summary>
public readonly struct RelativePath : IPath, IEquatable<RelativePath>, IComparable<RelativePath>
{
    /// <summary>
    /// Represents an empty relative path.
    /// </summary>
    public static readonly RelativePath Empty = new(string.Empty);

    /// <summary>
    /// The internal path string using backslash separators.
    /// </summary>
    public readonly string Path;

    /// <summary>
    /// Gets the file extension (including the dot).
    /// </summary>
    public Extension Extension => Extension.FromPath(Path);

    /// <summary>
    /// Gets the file name (last component of the path).
    /// </summary>
    public RelativePath FileName => new(PathHelpers.GetFileName(Path, PathHelpers.BackSlash).ToString());

    /// <summary>
    /// Gets the parent directory.
    /// </summary>
    public RelativePath Parent
    {
        get
        {
            var dir = PathHelpers.GetDirectoryName(Path, PathHelpers.BackSlash);
            return dir.IsEmpty ? Empty : new RelativePath(dir.ToString());
        }
    }

    /// <summary>
    /// Gets the depth (number of path components).
    /// </summary>
    public int Depth => string.IsNullOrEmpty(Path) ? 0 : PathHelpers.GetDepth(Path, PathHelpers.BackSlash);

    /// <summary>
    /// Gets the number of directory levels (same as Depth).
    /// </summary>
    public int Level => Depth;

    /// <summary>
    /// Gets the top-level parent (first path component).
    /// </summary>
    public RelativePath TopParent
    {
        get
        {
            var top = PathHelpers.GetTopParent(Path, PathHelpers.BackSlash);
            return top.IsEmpty ? Empty : new RelativePath(top.ToString());
        }
    }

    /// <summary>
    /// Gets the file name without the extension.
    /// </summary>
    public RelativePath FileNameWithoutExtension
    {
        get
        {
            var fileName = PathHelpers.GetFileName(Path, PathHelpers.BackSlash);
            if (fileName.IsEmpty) return Empty;
            var ext = PathHelpers.GetExtension(fileName);
            if (ext.IsEmpty) return new RelativePath(fileName.ToString());
            return new RelativePath(fileName.Slice(0, fileName.Length - ext.Length).ToString());
        }
    }

    /// <summary>
    /// Creates a RelativePath from a sanitized string (must already use backslashes).
    /// </summary>
    internal RelativePath(string path)
    {
        Path = path ?? string.Empty;
    }

    /// <summary>
    /// Creates a RelativePath from parts.
    /// </summary>
    public static RelativePath FromParts(string[] parts)
    {
        if (parts == null || parts.Length == 0) return Empty;
        return new RelativePath(string.Join(PathHelpers.BackSlash, parts));
    }

    /// <summary>
    /// Parses a string into a RelativePath, normalizing separators.
    /// </summary>
    public static explicit operator RelativePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Empty;

        // Check for absolute path indicators
        if (input.Contains(':'))
            throw new PathException($"Tried to parse `{input}` but `:` is not valid in a relative path name");

        var sanitized = PathHelpers.SanitizeRelative(input);
        return new RelativePath(sanitized);
    }

    /// <summary>
    /// Converts a RelativePath to its string representation.
    /// </summary>
    public static explicit operator string(RelativePath path)
    {
        return path.Path;
    }

    /// <summary>
    /// Gets the path part at the specified index.
    /// </summary>
    public string GetPart(int index)
    {
        if (string.IsNullOrEmpty(Path)) throw new IndexOutOfRangeException();

        var parts = Path.Split(PathHelpers.BackSlash);
        return parts[index];
    }

    /// <summary>
    /// Gets all path parts as an array.
    /// </summary>
    public string[] GetParts()
    {
        if (string.IsNullOrEmpty(Path)) return Array.Empty<string>();
        return Path.Split(PathHelpers.BackSlash);
    }

    /// <summary>
    /// Returns a new path with the extension replaced.
    /// </summary>
    public RelativePath ReplaceExtension(Extension newExtension)
    {
        var newPath = PathHelpers.ReplaceExtension(Path, newExtension.ToString());
        return new RelativePath(newPath);
    }

    /// <summary>
    /// Returns a new path with the extension appended.
    /// </summary>
    public RelativePath WithExtension(Extension? ext)
    {
        if (ext == null) return this;
        return new RelativePath(Path + ext);
    }

    /// <summary>
    /// Returns a new path without the extension.
    /// </summary>
    public RelativePath WithoutExtension()
    {
        var ext = Extension;
        var extStr = ext.ToString();
        if (string.IsNullOrEmpty(extStr)) return this;
        return new RelativePath(Path.Substring(0, Path.Length - extStr.Length));
    }

    /// <summary>
    /// Combines this path with an AbsolutePath base, converting backslashes to forward slashes.
    /// </summary>
    public AbsolutePath RelativeTo(AbsolutePath basePath)
    {
        return basePath.Combine(this);
    }

    /// <summary>
    /// Checks if this path is within the specified parent folder.
    /// </summary>
    public bool InFolder(RelativePath parent)
    {
        return PathHelpers.InFolder(Path, parent.Path, PathHelpers.BackSlash);
    }

    /// <summary>
    /// Returns the portion of this path relative to the specified base path.
    /// </summary>
    public RelativePath RelativeTo(RelativePath basePath)
    {
        if (basePath == Empty) return this;
        var rel = PathHelpers.RelativeTo(Path, basePath.Path, PathHelpers.BackSlash);
        if (rel.IsEmpty && !PathHelpers.PathEquals(Path, basePath.Path))
            throw new PathException($"Path '{Path}' is not relative to '{basePath.Path}'");
        return new RelativePath(rel.ToString());
    }

    /// <summary>
    /// Combines this path with another relative path.
    /// </summary>
    public RelativePath Combine(params object[] paths)
    {
        var result = this;
        foreach (var p in paths)
        {
            var relPath = p switch
            {
                string s => (RelativePath)s,
                RelativePath rp => rp,
                _ => throw new PathException($"Cannot cast {p} of type {p.GetType()} to RelativePath")
            };
            result = result.Combine(relPath);
        }
        return result;
    }

    /// <summary>
    /// Combines this path with another relative path.
    /// </summary>
    public RelativePath Combine(params RelativePath[] paths)
    {
        if (paths == null || paths.Length == 0) return this;

        var result = Path;
        foreach (var p in paths)
        {
            result = PathHelpers.JoinParts(result, p.Path, PathHelpers.BackSlash);
        }
        return new RelativePath(result);
    }

    /// <summary>
    /// Returns the path as a string with backslash separators.
    /// </summary>
    public override string ToString()
    {
        return Path ?? string.Empty;
    }

    /// <summary>
    /// Converts backslashes to forward slashes for combining with AbsolutePath.
    /// </summary>
    internal string ToForwardSlash()
    {
        return PathHelpers.RelativeToForwardSlash(Path);
    }

    #region Equality and Comparison

    public override int GetHashCode()
    {
        return PathHelpers.PathHashCode(Path);
    }

    public bool Equals(RelativePath other)
    {
        return PathHelpers.PathEquals(Path, other.Path);
    }

    public override bool Equals(object? obj)
    {
        return obj is RelativePath other && Equals(other);
    }

    public static bool operator ==(RelativePath a, RelativePath b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(RelativePath a, RelativePath b)
    {
        return !a.Equals(b);
    }

    public int CompareTo(RelativePath other)
    {
        return PathHelpers.Compare(Path, other.Path);
    }

    #endregion

    #region String-like methods

    /// <summary>
    /// Checks if the path ends with the specified suffix.
    /// </summary>
    public bool EndsWith(string postfix)
    {
        return Path.EndsWith(postfix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the file name starts with the specified prefix.
    /// </summary>
    public bool FileNameStartsWith(string prefix)
    {
        var fileName = PathHelpers.GetFileName(Path, PathHelpers.BackSlash);
        return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the path starts with the specified prefix.
    /// </summary>
    public bool StartsWith(string prefix)
    {
        return Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this path starts with another relative path.
    /// </summary>
    public bool StartsWith(RelativePath other)
    {
        if (string.IsNullOrEmpty(other.Path)) return true;
        if (!Path.StartsWith(other.Path, StringComparison.OrdinalIgnoreCase)) return false;
        if (Path.Length == other.Path.Length) return true;
        return Path[other.Path.Length] == PathHelpers.BackSlash;
    }

    #endregion

    #region Obsolete compatibility properties

    /// <summary>
    /// Gets path parts as an array. Prefer GetParts() or GetPart(index) for new code.
    /// </summary>
    [Obsolete("Use GetParts() or GetPart(index) instead")]
    public string[] Parts => GetParts();

    #endregion
}
