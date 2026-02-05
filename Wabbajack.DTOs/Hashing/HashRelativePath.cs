using System;
using System.Linq;
using Wabbajack.Paths;

namespace Wabbajack.Hashing.xxHash64;

/// <summary>
/// Represents a hash followed by relative path segments,
/// typically used for referencing files within hashed archives.
/// </summary>
public readonly struct HashRelativePath : IPath, IEquatable<HashRelativePath>, IComparable<HashRelativePath>
{
    /// <summary>
    /// The base hash.
    /// </summary>
    public readonly Hash Hash;

    /// <summary>
    /// The relative path segments.
    /// </summary>
    public readonly RelativePath[] Parts;

    /// <summary>
    /// Gets the file extension.
    /// </summary>
    public Extension Extension => Parts.Length > 0
        ? Parts[^1].Extension
        : throw new InvalidOperationException("No path in HashRelativePath");

    /// <summary>
    /// Gets the file name.
    /// </summary>
    public RelativePath FileName => Parts.Length > 0
        ? Parts[^1].FileName
        : throw new InvalidOperationException("No path in HashRelativePath");

    /// <summary>
    /// Creates a HashRelativePath from a hash and relative parts.
    /// </summary>
    public HashRelativePath(Hash basePath, params RelativePath[] parts)
    {
        Hash = basePath;
        Parts = parts ?? Array.Empty<RelativePath>();
    }

    /// <summary>
    /// Returns the string representation using | as separator.
    /// </summary>
    public override string ToString()
    {
        if (Parts.Length == 0) return Hash.ToString();
        return Hash + "|" + string.Join("|", Parts.Select(p => p.ToString()));
    }

    public override bool Equals(object? obj)
    {
        return obj is HashRelativePath path && Equals(path);
    }

    public override int GetHashCode()
    {
        var hash = Hash.GetHashCode();
        foreach (var part in Parts)
        {
            hash = hash ^ part.GetHashCode();
        }
        return hash;
    }

    public bool Equals(HashRelativePath other)
    {
        if (other.Parts.Length != Parts.Length) return false;
        if (other.Hash != Hash) return false;

        for (var i = 0; i < Parts.Length; i++)
        {
            if (!Parts[i].Equals(other.Parts[i])) return false;
        }
        return true;
    }

    public int CompareTo(HashRelativePath other)
    {
        var init = Hash.CompareTo(other.Hash);
        if (init != 0) return init;

        var minLength = Math.Min(Parts.Length, other.Parts.Length);
        for (var i = 0; i < minLength; i++)
        {
            var cmp = Parts[i].CompareTo(other.Parts[i]);
            if (cmp != 0) return cmp;
        }

        return Parts.Length.CompareTo(other.Parts.Length);
    }

    public static bool operator ==(HashRelativePath a, HashRelativePath b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(HashRelativePath a, HashRelativePath b)
    {
        return !a.Equals(b);
    }
}
