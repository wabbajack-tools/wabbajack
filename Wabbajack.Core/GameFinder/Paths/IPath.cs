using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// Abstracts an individual path.
/// </summary>
public interface IPath
{
    /// <summary>
    /// Gets the extension of this path.
    /// </summary>
    Extension Extension { get; }

    /// <summary>
    /// Gets the file name of this path.
    /// </summary>
    RelativePath FileName { get; }
}

/// <summary>
/// Abstracts an individual path.
/// Allows methods to return specific path types.
/// </summary>
/// <typeparam name="TConcretePath">Concrete path type returned by method implementations</typeparam>
[PublicAPI]
public interface IPath<TConcretePath> : IPath where TConcretePath : struct, IPath<TConcretePath>, IEquatable<TConcretePath>
{
    /// <summary>
    /// The file name of this path.
    /// </summary>
    /// <remarks>
    /// Returns an empty <see cref="RelativePath"/> if this path is a root component.
    /// </remarks>
    RelativePath Name { get; }

    /// <summary>
    /// Traverses one directory up, returning the path of the parent.
    /// </summary>
    /// <remarks>
    /// If the path is a root component, returns the root component.
    /// If path is not rooted and there are no parent directories, returns an empty path.
    /// </remarks>
    TConcretePath Parent { get; }

    /// <summary>
    /// If this path is rooted, returns the root component, an empty path otherwise.
    /// </summary>
    TConcretePath GetRootComponent { get; }

    /// <summary>
    /// Returns a collection of parts that make up this path, excluding root components.
    /// </summary>
    /// <remarks>
    /// Root components like `C:/` are excluded and should be handled separately.
    /// </remarks>
    IEnumerable<RelativePath> Parts { get; }

    /// <summary>
    /// Returns a collection of all parent paths, including this path.
    /// </summary>
    /// <remarks>
    /// Order is from this path to the root.
    /// </remarks>
    IEnumerable<TConcretePath> GetAllParents();

    /// <summary>
    /// Returns a <see cref="RelativePath"/> of the non-root part of this path.
    /// </summary>
    RelativePath GetNonRootPart();

    /// <summary>
    /// Returns whether this path is rooted.
    /// </summary>
    bool IsRooted { get; }

    /// <summary>
    /// Returns true if this path is a child of the specified path.
    /// </summary>
    /// <param name="parent">The potential parent path</param>
    /// <remarks>The child path needs to have greater depth than the parent.</remarks>
    /// <returns>True if this is a child path of the parent path; else false.</returns>
    bool InFolder(TConcretePath parent);

    /// <summary>
    /// Returns true if this path starts with the specified path.
    /// </summary>
    /// <param name="other">The prefix path</param>
    bool StartsWith(TConcretePath other);

    /// <summary>
    /// Returns true if this path ends with the specified RelativePath.
    /// </summary>
    /// <remarks>Since RelativePaths can't contain Root components, this check won't consider root folders</remarks>
    /// <param name="other">The relative path with which this path is supposed to end</param>
    bool EndsWith(RelativePath other);
}
