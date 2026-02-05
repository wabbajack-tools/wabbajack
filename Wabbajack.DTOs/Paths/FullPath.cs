using System;
using System.Linq;

namespace Wabbajack.Paths;

public readonly struct FullPath : IPath, IEquatable<FullPath>, IComparable<FullPath>
{
    public readonly AbsolutePath Base;
    public readonly RelativePath[] Parts;


    public Extension Extension => Parts.Length > 0 ? Parts[^1].Extension : Base.Extension;
    public RelativePath FileName => Parts.Length > 0 ? Parts[^1].FileName : Base.FileName;

    public FullPath(AbsolutePath basePath, params RelativePath[] parts)
    {
        Base = basePath;
        Parts = parts;
    }

    public override string ToString()
    {
        return Base + "|" + string.Join("|", Parts);
    }

    public override bool Equals(object? obj)
    {
        return obj is FullPath path && Equals(path);
    }

    public override int GetHashCode()
    {
        return Parts.Aggregate(Base.GetHashCode(), (i, path) => i ^ path.GetHashCode());
    }

    public bool Equals(FullPath other)
    {
        if (other.Parts.Length != Parts.Length) return false;
        if (other.Base != Base) return false;
        return ArrayExtensions.AreEqual(Parts, 0, other.Parts, 0, Parts.Length);
    }

    public int CompareTo(FullPath other)
    {
        var init = Base.CompareTo(other.Base);
        if (init != 0) return init;
        return ArrayExtensions.Compare(Parts, other.Parts);
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
    ///     Creates a new full path, with relativePath combined with the deepest leaf in the full path
    /// </summary>
    /// <param name="relativePath"></param>
    /// <returns></returns>
    public FullPath InSameFolder(RelativePath relativePath)
    {
        if (Parts.Length == 0) return new FullPath(Base.Parent.Combine(relativePath));

        var paths = new RelativePath[Parts.Length];
        Parts.CopyTo(paths, 0);
        paths[^1] = paths[^1].Parent.Combine(relativePath);
        return new FullPath(Base, paths);
    }
}