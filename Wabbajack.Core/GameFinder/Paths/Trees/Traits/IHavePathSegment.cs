using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wabbajack.GameFinder.Paths.Extensions;
using static Reloaded.Memory.Extensions.SpanExtensions;

namespace Wabbajack.GameFinder.Paths.Trees.Traits;

/// <summary>
///     Represents nodes which have a path segment contained within.
///     A path segment being a sub-path for the given directory in a file tree.
/// </summary>
public interface IHavePathSegment
{
    /// <summary>
    ///     The path segment of the current node.
    /// </summary>
    public RelativePath Segment { get; }
}

/// <summary>
///     Extensions tied to the <see cref="IHavePathSegment" />
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHavePathSegmentExtensions
{
    /// <summary>
    ///     Retrieves the path segment of the node.
    /// </summary>
    /// <param name="item">The keyed boxed node whose key is to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <returns>The key of the node.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static RelativePath Segment<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHavePathSegment
        where TKey : notnull
        => item.Value.Item.Segment;

    /// <summary>
    ///     Retrieves the path segment of the node.
    /// </summary>
    /// <param name="item">The keyed boxed node whose key is to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <returns>The key of the node.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static RelativePath Segment<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHavePathSegment
        where TKey : notnull
        => item.Item.Segment;

    /// <summary>
    ///     Retrieves the path segment of the node.
    /// </summary>
    /// <param name="item">The boxed node whose key is to be retrieved.</param>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>The key of the node.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static RelativePath Segment<TSelf>(this Box<TSelf> item)
        where TSelf : struct, IHavePathSegment
        => item.Item.Segment;

    /// <summary>
    ///     Reconstructs the full path of the current node by walking up to the root to the tree.
    /// </summary>
    /// <param name="item">The node for which to reconstruct the file path from.</param>
    public static RelativePath ReconstructPath<TSelf>(this TSelf item)
        where TSelf : struct, IHavePathSegment, IHaveParent<TSelf>
    {
        // Remember to follow the rules:
        // Pre-calculate the length of the path.
        var pathLength = item.Segment.Length;

        // Start with the current item and walk up the tree.
        var currentItem = item;
        while (currentItem.HasParent)
        {
            // Each parent adds its own segment length plus one for the separator.
            var len = currentItem.Parent!.Item.Segment.Length;
            var isEmpty = len > 0; // this makes the separator not add to length if the segment is empty, in case of empty root.
            pathLength += len + (Unsafe.As<bool, byte>(ref isEmpty) * 1);
            currentItem = currentItem.Parent.Item;
        }

        // Create the string.
        static void Action(Span<char> span, TSelf item)
        {
            var currentItem = item;
            var position = span.Length;

            // Append the current (leaf) segment first.
            var segmentSpan = currentItem.Segment.Path.AsSpan();
            position -= segmentSpan.Length;
            segmentSpan.CopyTo(span.SliceFast(position));

            // Walk up the tree to build the path.
            while (currentItem.HasParent && currentItem.Parent!.Segment().Length > 0)
            {
                // Add the path separator.
                span.DangerousGetReferenceAt(--position) = '/';

                // Get the parent segment.
                currentItem = currentItem.Parent!.Item; // ignored because has parent
                segmentSpan = currentItem.Segment.Path.AsSpan();

                // Copy the parent segment.
                position -= segmentSpan.Length;
                segmentSpan.CopyTo(span.SliceFast(position));
            }
        }

        return string.Create(pathLength, item, Action);
    }
}

/// <summary>
///     Extensions tied to <see cref="IHavePathSegment" /> and <see cref="IHaveBoxedChildren{TSelf}"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHavePathSegmentExtensionsForIHaveBoxedChildren
{
    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The full relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    public static Box<TSelf>? FindByPathFromRoot<TSelf>(this Box<TSelf> root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHavePathSegment
    {
        var start = 0; // Start index of the current segment.
        return FindNodeRecursive(root, fullPath.Path.AsSpan(), ref start);
    }

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    [ExcludeFromCodeCoverage] // Wrapper
    [Obsolete("This method causes temporary boxing of object. Do not use unless you have no other way to access this item. Use this method via Box<TSelf> instead.")]
    public static Box<TSelf>? FindByPathFromRoot<TSelf>(this TSelf root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHavePathSegment
        => FindByPathFromRoot((Box<TSelf>) root, fullPath);

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Starting with child nodes, not including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The full relative path from this node (starting with child) to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    public static Box<TSelf>? FindByPathFromChild<TSelf>(this Box<TSelf> root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHavePathSegment =>
        root.Item.FindByPathFromChild(fullPath);

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Starting with child nodes, not including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The relative path from this node (starting with child) to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    public static Box<TSelf>? FindByPathFromChild<TSelf>(this TSelf root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHavePathSegment
    {
        var start = 0; // Start index of the current segment.
        foreach (var child in root.Children)
        {
            var found = FindNodeRecursive(child, fullPath.Path.AsSpan(), ref start);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Box<TSelf>? FindNodeRecursive<TSelf>(Box<TSelf> node, ReadOnlySpan<char> fullPath, ref int start)
        where TSelf : struct, IHaveBoxedChildren<TSelf>, IHavePathSegment
    {
        // Find the end of the current segment.
        var end = fullPath.SliceFast(start).IndexOf('/');
        var currentSegment = end == -1 ? fullPath.SliceFast(start) : fullPath.SliceFast(start, end);

        // Compare the current segment with the node's segment.
        if (!currentSegment.Equals(node.Item.Segment.Path.AsSpan(), StringComparison.OrdinalIgnoreCase))
            return null;

        // Update start to the next segment.
        start += currentSegment.Length;
        if (end != -1)
            start++; // Skip the '/'

        // If this is the last segment, return the current node.
        if (start >= fullPath.Length)
            return node;

        // Otherwise, search the children.
        foreach (var child in node.Item.Children)
        {
            var found = FindNodeRecursive(child, fullPath, ref start);
            if (found != null)
                return found;
        }

        // If no matching child is found, return null.
        return null;
    }
}

/// <summary>
///     Extensions tied to <see cref="IHavePathSegment" /> and <see cref="IHaveObservableChildren{TSelf}"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHavePathSegmentExtensionsForIHaveObservableChildren
{
    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The full relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    public static Box<TSelf>? FindByPathFromRoot<TSelf>(this Box<TSelf> root, RelativePath fullPath)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHavePathSegment
    {
        var start = 0; // Start index of the current segment.
        return FindNodeRecursive(root, fullPath.Path.AsSpan(), ref start);
    }

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    [Obsolete("This method causes temporary boxing of object. Do not use unless you have no other way to access this item. Use this method via Box<TSelf> instead.")]
    public static Box<TSelf>? FindByPathFromRoot<TSelf>(this TSelf root, RelativePath fullPath)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHavePathSegment
        => FindByPathFromRoot((Box<TSelf>) root, fullPath);

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Starting with child nodes, not including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The full relative path from this node (starting with child) to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    public static Box<TSelf>? FindByPathFromChild<TSelf>(this Box<TSelf> root, RelativePath fullPath)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHavePathSegment =>
        root.Item.FindByPathFromChild(fullPath);

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Starting with child nodes, not including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The relative path from this node (starting with child) to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    /// <remarks>
    ///     This is very slow, at O(N). If you need to use this with large trees, consider using the
    ///     dictionary variant based on <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" /> instead.
    /// </remarks>
    public static Box<TSelf>? FindByPathFromChild<TSelf>(this TSelf root, RelativePath fullPath)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHavePathSegment
    {
        var start = 0; // Start index of the current segment.
        foreach (var child in root.Children)
        {
            var found = FindNodeRecursive(child, fullPath.Path.AsSpan(), ref start);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Box<TSelf>? FindNodeRecursive<TSelf>(Box<TSelf> node, ReadOnlySpan<char> fullPath, ref int start)
        where TSelf : struct, IHaveObservableChildren<TSelf>, IHavePathSegment
    {
        // Find the end of the current segment.
        var end = fullPath.SliceFast(start).IndexOf('/');
        var currentSegment = end == -1 ? fullPath.SliceFast(start) : fullPath.SliceFast(start, end);

        // Compare the current segment with the node's segment.
        if (!currentSegment.Equals(node.Item.Segment.Path.AsSpan(), StringComparison.OrdinalIgnoreCase))
            return null;

        // Update start to the next segment.
        start += currentSegment.Length;
        if (end != -1)
            start++; // Skip the '/'

        // If this is the last segment, return the current node.
        if (start >= fullPath.Length)
            return node;

        // Otherwise, search the children.
        foreach (var child in node.Item.Children)
        {
            var found = FindNodeRecursive(child, fullPath, ref start);
            if (found != null)
                return found;
        }

        // If no matching child is found, return null.
        return null;
    }
}

/// <summary>
///     Extensions tied to <see cref="IHavePathSegment" /> and <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHavePathSegmentExtensionsForIHaveBoxedChildrenWithKey
{
    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree wrapped in a ChildWithKeyBox.</param>
    /// <param name="fullPath">The full relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    public static KeyedBox<RelativePath, TSelf>? FindByPathFromRoot<TSelf>(this KeyedBox<RelativePath, TSelf> root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildrenWithKey<RelativePath, TSelf>, IHavePathSegment
    {
        var pathParts = fullPath.Path.Split('/');
        if (!root.Item.Segment.Equals(pathParts.DangerousGetReferenceAt(0)))
            return null;

        return pathParts.Length == 1 ? root : FindNodeInternal(root, pathParts, 1);
    }

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The full relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    [ExcludeFromCodeCoverage]
    [Obsolete("This method causes temporary boxing of object. Do not use unless you have no other way to access this item. Use this method via Box<TSelf> instead.")]
    public static KeyedBox<RelativePath, TSelf>? FindByPathFromRoot<TSelf>(this TSelf root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildrenWithKey<RelativePath, TSelf>, IHavePathSegment
        => FindByPathFromRoot((KeyedBox<RelativePath, TSelf>) root, fullPath);

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Starting with child nodes, not including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree wrapped in a ChildWithKeyBox.</param>
    /// <param name="fullPath">The full relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    public static KeyedBox<RelativePath, TSelf>? FindByPathFromChild<TSelf>(this KeyedBox<RelativePath, TSelf> root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildrenWithKey<RelativePath, TSelf>, IHavePathSegment
    {
        var pathParts = fullPath.Path.Split('/');
        return FindNodeInternal(root, pathParts, 0);
    }

    /// <summary>
    ///     Finds a node in the tree based on a given relative path.
    ///     (Starting with child nodes, not including this/root node in the search)
    /// </summary>
    /// <param name="root">The current (search root) node of the tree.</param>
    /// <param name="fullPath">The full relative path from this node to find.</param>
    /// <typeparam name="TSelf">The type of the node in the tree.</typeparam>
    /// <returns>The node that matches the given full path, or null if not found.</returns>
    [ExcludeFromCodeCoverage]
    [Obsolete("This method causes temporary boxing of object. Do not use unless you have no other way to access this item. Use this method via Box<TSelf> instead.")]
    public static KeyedBox<RelativePath, TSelf>? FindByPathFromChild<TSelf>(this TSelf root, RelativePath fullPath)
        where TSelf : struct, IHaveBoxedChildrenWithKey<RelativePath, TSelf>, IHavePathSegment
        => FindByPathFromChild((KeyedBox<RelativePath, TSelf>)root, fullPath);

    private static KeyedBox<RelativePath, TSelf>? FindNodeInternal<TSelf>(KeyedBox<RelativePath, TSelf> node, string[] pathParts, int index) where TSelf : struct, IHaveBoxedChildrenWithKey<RelativePath, TSelf>, IHavePathSegment
    {
        while (true)
        {
            if (index >= pathParts.Length)
                return node;

            var currentSegment = new RelativePath(pathParts.DangerousGetReferenceAt(index));

            // Check if the current node has a child with the extracted key.
            if (!node.Item.Children.TryGetValue(currentSegment, out var childBox))
                return null;

            // Recursively search the child node.
            node = childBox;
            index += 1;
        }
    }
}
