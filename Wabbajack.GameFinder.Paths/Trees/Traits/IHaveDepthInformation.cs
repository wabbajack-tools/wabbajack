using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Wabbajack.GameFinder.Paths.Trees.Traits;

/// <summary>
///     Represents trees with embedded depth information for each node.
/// </summary>
public interface IHaveDepthInformation
{
    /// <summary>
    ///     Returns the depth of the node in the tree, with the root node having a depth of 0.
    ///     So, in a FileTree `bar` in `/foo/bar/baz` will have a depth of 2 due to the `/` having a depth of 0.
    /// </summary>
    public ushort Depth { get; }
}

/// <summary>
///     Trait methods for <see cref="IHaveObservableChildren{TSelf}" />.
/// </summary>
[ExcludeFromCodeCoverage] // Wrapper
// ReSharper disable once InconsistentNaming
public static class IHaveDepthInformationExtensions
{
    /// <summary>
    ///     Retrieves the depth of the node in the tree structure.
    /// </summary>
    /// <param name="item">The boxed node whose depth is to be retrieved.</param>
    /// <returns>The depth of the node.</returns>
    public static ushort Depth<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHaveDepthInformation
        => item.Item.Depth;

    /// <summary>
    ///     Retrieves the depth of the node in the tree structure.
    /// </summary>
    /// <param name="item">The keyed boxed node whose depth is to be retrieved.</param>
    /// <returns>The depth of the node.</returns>
    public static ushort Depth<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveDepthInformation
        where TKey : notnull
        => item.Item.Depth;

    /// <summary>
    ///     Retrieves the depth of the node in the tree structure.
    /// </summary>
    /// <param name="item">The keyed boxed node whose depth is to be retrieved.</param>
    /// <returns>The depth of the node.</returns>
    public static ushort Depth<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveDepthInformation
        where TKey : notnull
        => item.Value.Item.Depth;
}
