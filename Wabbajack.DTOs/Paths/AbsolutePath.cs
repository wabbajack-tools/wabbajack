using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Wabbajack.Paths;

/// <summary>
/// An absolute path that uses forward slashes internally.
/// Paths are stored as Directory + FileName strings with forward slash separators.
/// </summary>
public readonly struct AbsolutePath : IPath, IComparable<AbsolutePath>, IEquatable<AbsolutePath>
{
    /// <summary>
    /// Represents an empty/default absolute path.
    /// </summary>
    public static readonly AbsolutePath Empty = default;

    /// <summary>
    /// The directory component of the path (forward slashes, e.g., "C:/Games/Skyrim" or "/home/user").
    /// </summary>
    public readonly string Directory;

    /// <summary>
    /// The file name component of the path as a string (e.g., "SkyrimSE.exe").
    /// </summary>
    internal readonly string FileNameString;

    /// <summary>
    /// Gets the file extension (including the dot).
    /// </summary>
    public Extension Extension => string.IsNullOrEmpty(FileNameString)
        ? Extension.FromPath(Directory)
        : Extension.FromPath(FileNameString);

    /// <summary>
    /// Gets the file name as a RelativePath (for backward compatibility).
    /// </summary>
    public RelativePath FileName => string.IsNullOrEmpty(FileNameString)
        ? RelativePath.Empty
        : new RelativePath(FileNameString);

    /// <summary>
    /// Gets the parent directory.
    /// </summary>
    public AbsolutePath Parent
    {
        get
        {
            var fullPath = GetFullPath();
            // Check if we're already at root - roots have no parent
            if (IsRoot(fullPath))
                throw new PathException($"Path {this} does not have a parent folder");
            var parentDir = PathHelpers.GetDirectoryName(fullPath, PathHelpers.ForwardSlash);
            if (parentDir.IsEmpty)
                throw new PathException($"Path {this} does not have a parent folder");
            return FromSanitized(parentDir.ToString());
        }
    }

    private static bool IsRoot(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        // Unix root: "/"
        if (path == "/") return true;
        // DOS root: "C:/"
        if (path.Length == 3 && PathHelpers.IsValidWindowsDriveChar(path[0]) && path[1] == ':' && path[2] == '/')
            return true;
        return false;
    }

    /// <summary>
    /// Gets the depth (number of path components).
    /// </summary>
    public int Depth
    {
        get
        {
            var fullPath = GetFullPath();
            return string.IsNullOrEmpty(fullPath) ? 0 : PathHelpers.GetDepth(fullPath, PathHelpers.ForwardSlash);
        }
    }

    /// <summary>
    /// Creates an AbsolutePath from sanitized Directory and FileName components.
    /// </summary>
    internal AbsolutePath(string directory, string fileName)
    {
        Directory = directory ?? string.Empty;
        FileNameString = fileName ?? string.Empty;
    }

    /// <summary>
    /// Gets the full path by combining Directory and FileName.
    /// </summary>
    public string GetFullPath()
    {
        if (string.IsNullOrEmpty(Directory)) return string.Empty;
        if (string.IsNullOrEmpty(FileNameString)) return Directory;
        return PathHelpers.JoinParts(Directory, FileNameString, PathHelpers.ForwardSlash);
    }

    /// <summary>
    /// Creates an AbsolutePath from a sanitized full path string.
    /// </summary>
    internal static AbsolutePath FromSanitized(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return Empty;
        var dir = PathHelpers.GetDirectoryName(fullPath, PathHelpers.ForwardSlash);
        var file = PathHelpers.GetFileName(fullPath, PathHelpers.ForwardSlash);
        return new AbsolutePath(dir.ToString(), file.ToString());
    }

    /// <summary>
    /// Parses a string into an AbsolutePath, normalizing separators.
    /// </summary>
    public static explicit operator AbsolutePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Empty;
        var sanitized = PathHelpers.SanitizeAbsolute(input);
        ValidateAbsolutePath(sanitized);
        return FromSanitized(sanitized);
    }

    private static void ValidateAbsolutePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        // Unix path
        if (path.StartsWith("/")) return;

        // UNC path (now using forward slashes after sanitization)
        if (path.StartsWith("//")) return;

        // DOS path
        if (path.Length >= 3 && PathHelpers.IsValidWindowsDriveChar(path[0]) && path[1] == ':' && path[2] == '/')
            return;

        throw new PathException($"Invalid absolute path format: {path}");
    }

    /// <summary>
    /// Returns the full path string with forward slashes.
    /// </summary>
    public override string ToString()
    {
        var fullPath = GetFullPath();
        return fullPath ?? string.Empty;
    }

    /// <summary>
    /// Returns the path with native platform separators.
    /// </summary>
    public string ToNativePath()
    {
        return PathHelpers.ToNativeSeparators(GetFullPath(), OperatingSystem.IsWindows());
    }

    /// <summary>
    /// Enumerates this path and all its parents.
    /// </summary>
    public IEnumerable<AbsolutePath> ThisAndAllParents()
    {
        var p = this;
        while (true)
        {
            yield return p;
            if (p.Depth <= 1)
                yield break;
            p = p.Parent;
        }
    }

    /// <summary>
    /// Returns a new path with the extension replaced.
    /// </summary>
    public AbsolutePath ReplaceExtension(Extension newExtension)
    {
        var fullPath = GetFullPath();
        var newPath = PathHelpers.ReplaceExtension(fullPath, newExtension.ToString());
        return FromSanitized(newPath);
    }

    /// <summary>
    /// Returns a new path with the extension appended.
    /// </summary>
    public AbsolutePath WithExtension(Extension? ext)
    {
        if (ext == null) return this;
        return FromSanitized(GetFullPath() + ext);
    }

    /// <summary>
    /// Returns a new path with text appended to the file name (before extension).
    /// </summary>
    public AbsolutePath AppendToName(string append)
    {
        var name = string.IsNullOrEmpty(FileNameString)
            ? PathHelpers.GetFileName(Directory, PathHelpers.ForwardSlash).ToString()
            : FileNameString;
        var ext = Extension;
        var nameWithoutExt = PathHelpers.ReplaceExtension(name, string.Empty);
        var newName = nameWithoutExt + append + ext;
        return Parent.Combine((RelativePath)newName);
    }

    /// <summary>
    /// Returns the portion of this path relative to the specified base path.
    /// </summary>
    public RelativePath RelativeTo(AbsolutePath basePath)
    {
        var child = GetFullPath();
        var parent = basePath.GetFullPath();

        if (!PathHelpers.InFolder(child, parent, PathHelpers.ForwardSlash))
            throw new PathException($"{basePath} is not a base path of {this}");

        var rel = PathHelpers.RelativeTo(child, parent, PathHelpers.ForwardSlash);
        // Convert forward slashes back to backslashes for RelativePath
        return new RelativePath(rel.ToString().Replace(PathHelpers.ForwardSlash, PathHelpers.BackSlash));
    }

    /// <summary>
    /// Checks if this path is within the specified parent folder.
    /// </summary>
    public bool InFolder(AbsolutePath parent)
    {
        return PathHelpers.InFolder(GetFullPath(), parent.GetFullPath(), PathHelpers.ForwardSlash);
    }

    /// <summary>
    /// Combines this path with relative paths, converting backslashes to forward slashes.
    /// </summary>
    public AbsolutePath Combine(params object[] paths)
    {
        var result = this;
        foreach (var p in paths)
        {
            var relPath = p switch
            {
                string s => (RelativePath)s,
                RelativePath rp => rp,
                _ => throw new PathException($"Cannot cast {p} of type {p.GetType()} to Path")
            };
            result = result.Combine(relPath);
        }
        return result;
    }

    /// <summary>
    /// Combines this path with relative paths, converting backslashes to forward slashes.
    /// </summary>
    public AbsolutePath Combine(params RelativePath[] paths)
    {
        if (paths == null || paths.Length == 0) return this;

        var result = GetFullPath();
        foreach (var p in paths)
        {
            var forwardSlashPath = p.ToForwardSlash();
            result = PathHelpers.JoinParts(result, forwardSlashPath, PathHelpers.ForwardSlash);
        }
        return FromSanitized(result);
    }

    /// <summary>
    /// Combines this path with a single relative path.
    /// </summary>
    public AbsolutePath Combine(RelativePath path)
    {
        var forwardSlashPath = path.ToForwardSlash();
        var result = PathHelpers.JoinParts(GetFullPath(), forwardSlashPath, PathHelpers.ForwardSlash);
        return FromSanitized(result);
    }

    /// <summary>
    /// Parses a string to AbsolutePath, returning Empty on failure instead of throwing.
    /// </summary>
    public static AbsolutePath ConvertNoFailure(string value)
    {
        try
        {
            return (AbsolutePath)value;
        }
        catch
        {
            return Empty;
        }
    }

    #region Equality and Comparison

    public override int GetHashCode()
    {
        return PathHelpers.PathHashCode(GetFullPath());
    }

    public bool Equals(AbsolutePath other)
    {
        return PathHelpers.PathEquals(GetFullPath(), other.GetFullPath());
    }

    public override bool Equals(object? obj)
    {
        return obj is AbsolutePath other && Equals(other);
    }

    public static bool operator ==(AbsolutePath a, AbsolutePath b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(AbsolutePath a, AbsolutePath b)
    {
        return !a.Equals(b);
    }

    public int CompareTo(AbsolutePath other)
    {
        return PathHelpers.Compare(GetFullPath(), other.GetFullPath());
    }

    #endregion

    #region Obsolete compatibility properties

    /// <summary>
    /// Gets path parts as an array. Use GetPart(index) for new code.
    /// </summary>
    [Obsolete("Use GetPart(index) or GetFullPath() instead")]
    public string[] Parts => string.IsNullOrEmpty(GetFullPath())
        ? Array.Empty<string>()
        : GetFullPath().Split(PathHelpers.ForwardSlash, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// For backward compatibility - use GetFullPath() instead.
    /// </summary>
    [Obsolete("Use GetFullPath() instead")]
    public string[] PathParts => Parts;

    #endregion
}
