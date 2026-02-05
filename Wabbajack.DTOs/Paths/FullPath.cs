using System;
using System.Linq;

namespace Wabbajack.Paths;

/// <summary>
/// Represents an absolute path followed by one or more relative path segments,
/// typically used for paths within archives.
/// </summary>
public readonly struct FullPath : IPath, IEquatable<FullPath>, IComparable<FullPath>
{
    /// <summary>
    /// The base absolute path.
    /// </summary>
    public readonly AbsolutePath Base;

    /// <summary>
    /// The relative path segments (e.g., paths within nested archives).
    /// </summary>
    public readonly RelativePath[] Parts;

    /// <summary>
    /// Gets the file extension.
    /// </summary>
    public Extension Extension => Parts.Length > 0 ? Parts[^1].Extension : Base.Extension;

    /// <summary>
    /// Gets the file name.
    /// </summary>
    public RelativePath FileName => Parts.Length > 0 ? Parts[^1].FileName : Base.FileName;

    /// <summary>
    /// Creates a FullPath from an absolute path and relative parts.
    /// </summary>
    public FullPath(AbsolutePath basePath, params RelativePath[] parts)
    {
        Base = basePath;
        Parts = parts ?? Array.Empty<RelativePath>();
    }

    /// <summary>
    /// Returns the string representation using | as separator.
    /// </summary>
    public override string ToString()
    {
        if (Parts.Length == 0) return Base.ToString();
        return Base + "|" + string.Join("|", Parts.Select(p => p.ToString()));
    }

    public override bool Equals(object? obj)
    {
        return obj is FullPath path && Equals(path);
    }

    public override int GetHashCode()
    {
        var hash = Base.GetHashCode();
        foreach (var part in Parts)
        {
            hash = hash ^ part.GetHashCode();
        }
        return hash;
    }

    public bool Equals(FullPath other)
    {
        if (other.Parts.Length != Parts.Length) return false;
        if (other.Base != Base) return false;

        for (var i = 0; i < Parts.Length; i++)
        {
            if (!Parts[i].Equals(other.Parts[i])) return false;
        }
        return true;
    }

    public int CompareTo(FullPath other)
    {
        var init = Base.CompareTo(other.Base);
        if (init != 0) return init;

        var minLength = Math.Min(Parts.Length, other.Parts.Length);
        for (var i = 0; i < minLength; i++)
        {
            var cmp = Parts[i].CompareTo(other.Parts[i]);
            if (cmp != 0) return cmp;
        }

        return Parts.Length.CompareTo(other.Parts.Length);
    }

    public static bool operator ==(FullPath a, FullPath b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(FullPath a, FullPath b)
    {
        return !a.Equals(b);
    }

    /// <summary>
    /// Creates a new full path, with relativePath combined with the deepest leaf in the full path.
    /// </summary>
    public FullPath InSameFolder(RelativePath relativePath)
    {
        if (Parts.Length == 0) return new FullPath(Base.Parent.Combine(relativePath));

        var paths = new RelativePath[Parts.Length];
        Parts.CopyTo(paths, 0);
        paths[^1] = paths[^1].Parent.Combine(relativePath);
        return new FullPath(Base, paths);
    }
}
