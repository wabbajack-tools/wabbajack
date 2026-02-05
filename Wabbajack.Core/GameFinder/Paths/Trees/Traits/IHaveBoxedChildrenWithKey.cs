using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wabbajack.GameFinder.Paths.Trees.Traits.Interfaces;
using Reloaded.Memory.Extensions;

namespace Wabbajack.GameFinder.Paths.Trees.Traits;

/// <summary>
///     An interface used by Tree implementations to indicate that they have a keyed child.
/// </summary>
/// <typeparam name="TKey">The name of the key used in the File Tree.</typeparam>
/// <typeparam name="TSelf">The type of the child stored in this FileTree.</typeparam>
public interface IHaveBoxedChildrenWithKey<TKey, TSelf>
    where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
    where TKey : notnull
{
    /// <summary>
    ///     A Dictionary containing all the children of this node.
    /// </summary>
    /// <remarks>
    ///     This should point to an empty dictionary if there are no items.
    /// </remarks>
    public Dictionary<TKey, KeyedBox<TKey, TSelf>> Children { get; }
}

/// <summary>
///     Trait methods for <see cref="IHaveBoxedChildrenWithKey{TKey,TSelf}" />.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IHaveBoxedChildrenWithKeyExtensions
{
    /// <summary>
    ///     True if the current node is a leaf (it has no children).
    /// </summary>
    /// <param name="item">The node to check.</param>
    public static bool IsLeaf<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Item.IsLeaf<TSelf, TKey>();

    /// <summary>
    ///     True if the current node is a leaf (it has no children).
    /// </summary>
    /// <param name="item">The node to check.</param>
    public static bool IsLeaf<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Children.Count == 0;

    /// <summary>
    ///     Counts the number of children that match the given filter under this node.
    /// </summary>
    /// <param name="item">The node whose children are to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <returns>The total count of children that match the filter under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [ExcludeFromCodeCoverage] // Wrapper
    public static int CountChildren<TSelf, TKey, TFilter>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
        => item.Item.CountChildren<TSelf, TKey, TFilter>();

    /// <summary>
    ///     Counts the number of children that match the given filter under this node.
    /// </summary>
    /// <param name="item">The node whose children are to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <returns>The total count of children that match the filter under this node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountChildren<TSelf, TKey, TFilter>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
    {
        var result = 0;
        item.CountChildrenRecursive<TSelf, TKey, TFilter>(ref result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountChildrenRecursive<TSelf, TKey, TFilter>(this TSelf item, ref int accumulator)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
    {
        foreach (var child in item.Children)
        {
            var matchesFilter = TFilter.Match(child.Value.Item);
            accumulator += Unsafe.As<bool, byte>(ref matchesFilter); // Branchless increment.
            child.Value.Item.CountChildrenRecursive<TSelf, TKey, TFilter>(ref accumulator);
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenDfs<TSelf, TKey>(
        this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Item.EnumerateChildrenDfs<TSelf, TKey>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenDfs<TSelf, TKey>(
        this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        foreach (var child in item.Children)
        {
            yield return child;
            foreach (var grandChild in child.Value.Item.EnumerateChildrenDfs<TSelf, TKey>())
                yield return grandChild;
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenBfs<TSelf, TKey>(
        this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Item.EnumerateChildrenBfs<TSelf, TKey>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An IEnumerable of all child nodes of the current node.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenBfs<TSelf, TKey>(
        this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        var queue = new Queue<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>();
        foreach (var child in item.Children)
            queue.Enqueue(child);

        while (queue.TryDequeue(out var current))
        {
            yield return current;
            foreach (var grandChild in current.Value.Item.Children)
                queue.Enqueue(grandChild);
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner that satisfy a given filter.
    /// </summary>
    /// <param name="item">The node, wrapped in a KeyedBox, whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <returns>An IEnumerable of all filtered child nodes of the current node.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenBfs<TSelf, TKey, TFilter>(
        this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
        => item.Item.EnumerateChildrenBfs<TSelf, TKey, TFilter>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner that satisfy a given filter.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <returns>An IEnumerable of all filtered child nodes of the current node.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenBfs<TSelf, TKey, TFilter>(
        this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
    {
        var queue = new Queue<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>();
        foreach (var child in item.Children)
            queue.Enqueue(child);

        while (queue.TryDequeue(out var current))
        {
            if (TFilter.Match(current))
                yield return current;

            foreach (var grandChild in current.Value.Item.Children)
                queue.Enqueue(grandChild);
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner that satisfy a given filter.
    /// </summary>
    /// <param name="item">The node, wrapped in a KeyedBox, whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <returns>An IEnumerable of all filtered child nodes of the current node.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenDfs<TSelf, TKey, TFilter>(
        this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
        => item.Item.EnumerateChildrenDfs<TSelf, TKey, TFilter>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner that satisfy a given filter.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <returns>An IEnumerable of all filtered child nodes of the current node.</returns>
    public static IEnumerable<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>> EnumerateChildrenDfs<TSelf, TKey, TFilter>(
        this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
    {
        foreach (var child in item.Children)
        {
            if (TFilter.Match(child))
                yield return child;

            foreach (var grandChild in child.Value.Item.EnumerateChildrenDfs<TSelf, TKey, TFilter>())
                yield return grandChild;
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner, transforming each child node using the provided selector.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed child nodes of the current node.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static IEnumerable<TResult> EnumerateChildrenBfs<TSelf, TKey, TResult, TSelector>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
        => item.Item.EnumerateChildrenBfs<TSelf, TKey, TResult, TSelector>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner, transforming each child node using the provided selector.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed child nodes of the current node.</returns>
    public static IEnumerable<TResult> EnumerateChildrenBfs<TSelf, TKey, TResult, TSelector>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
    {
        var queue = new Queue<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>();
        foreach (var child in item.Children)
            queue.Enqueue(child);

        while (queue.TryDequeue(out var current))
        {
            yield return TSelector.Select(current);
            foreach (var grandChild in current.Value.Item.Children)
                queue.Enqueue(grandChild);
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner, transforming each child node using
    ///     the provided selector.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed child nodes of the current node.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static IEnumerable<TResult> EnumerateChildrenDfs<TSelf, TKey, TResult, TSelector>(
        this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
        => item.Item.EnumerateChildrenDfs<TSelf, TKey, TResult, TSelector>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner, transforming each child node using
    ///     the provided selector.
    /// </summary>
    /// <param name="item">The node whose children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed child nodes of the current node.</returns>
    public static IEnumerable<TResult> EnumerateChildrenDfs<TSelf, TKey, TResult, TSelector>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
    {
        foreach (var child in item.Children)
        {
            yield return TSelector.Select(child);
            foreach (var grandChild in child.Value.Item.EnumerateChildrenDfs<TSelf, TKey, TResult, TSelector>())
                yield return grandChild;
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner that satisfy a given filter,
    ///     transforming each filtered child node using the provided selector.
    /// </summary>
    /// <param name="item">The node whose filtered children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed and filtered child nodes of the current node.</returns>
    public static IEnumerable<TResult> EnumerateChildrenBfs<TSelf, TKey, TResult, TFilter, TSelector>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
        => item.Item.EnumerateChildrenBfs<TSelf, TKey, TResult, TFilter, TSelector>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a breadth-first manner that satisfy a given filter,
    ///     transforming each filtered child node using the provided selector.
    /// </summary>
    /// <param name="item">The node whose filtered children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed and filtered child nodes of the current node.</returns>
    public static IEnumerable<TResult> EnumerateChildrenBfs<TSelf, TKey, TResult, TFilter, TSelector>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
    {
        var queue = new Queue<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>();
        foreach (var child in item.Children)
            queue.Enqueue(child);

        while (queue.TryDequeue(out var current))
        {
            if (TFilter.Match(current))
                yield return TSelector.Select(current);

            foreach (var grandChild in current.Value.Item.Children)
                queue.Enqueue(grandChild);
        }
    }

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner that satisfy a given filter,
    ///     transforming each filtered child node using the provided selector.
    /// </summary>
    /// <param name="item">The node whose filtered children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed and filtered child nodes of the current node.</returns>
    public static IEnumerable<TResult> EnumerateChildrenDfs<TSelf, TKey, TResult, TFilter, TSelector>(
        this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
        => item.Item.EnumerateChildrenDfs<TSelf, TKey, TResult, TFilter, TSelector>();

    /// <summary>
    ///     Enumerates all child nodes of the current node in a depth-first manner that satisfy a given filter,
    ///     transforming each filtered child node using the provided selector.
    /// </summary>
    /// <param name="item">The node whose filtered children are to be enumerated and transformed.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter struct.</typeparam>
    /// <typeparam name="TResult">The result type after applying the selector.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An IEnumerable of transformed and filtered child nodes of the current node.</returns>
    public static IEnumerable<TResult> EnumerateChildrenDfs<TSelf, TKey, TResult, TFilter, TSelector>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>>
        where TSelector : struct, ISelector<KeyValuePair<TKey, KeyedBox<TKey, TSelf>>, TResult>
    {
        foreach (var child in item.Children)
        {
            if (TFilter.Match(child))
                yield return TSelector.Select(child);

            foreach (var grandChild in child.Value.Item.EnumerateChildrenDfs<TSelf, TKey, TResult, TFilter, TSelector>())
                yield return grandChild;
        }
    }

    /// <summary>
    ///     Counts the number of direct child nodes of the current node.
    /// </summary>
    /// <param name="item">The node whose children are to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The count of direct child nodes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountChildren<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Item.CountChildren<TSelf, TKey>();

    /// <summary>
    ///     Counts the number of direct child nodes of the current node.
    /// </summary>
    /// <param name="item">The node whose children are to be counted.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>The count of direct child nodes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountChildren<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        var result = 0;
        item.CountChildrenRecursive<TSelf, TKey>(ref result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountChildrenRecursive<TSelf, TKey>(this TSelf item, ref int accumulator)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf> where TKey : notnull
    {
        accumulator += item.Children.Count;
        foreach (var child in item.Children)
            child.Value.Item.CountChildrenRecursive<TSelf, TKey>(ref accumulator);
    }

    /// <summary>
    ///     Recursively returns all the child items of this node selected by the given selector.
    /// </summary>
    /// <param name="item">The boxed node with keyed children whose items to obtain.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An array of all the selected child items of this node.</returns>
    public static TResult[] GetChildrenRecursive<TSelf, TKey, TResult, TSelector>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<TSelf, TResult>
        => item.Item.GetChildrenRecursive<TSelf, TKey, TResult, TSelector>();

    /// <summary>
    ///     Recursively returns all the child items of this node selected by the given selector.
    /// </summary>
    /// <param name="item">The node with keyed children whose items to obtain.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An array of all the selected child items of this node.</returns>
    public static TResult[] GetChildrenRecursive<TSelf, TKey, TResult, TSelector>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<TSelf, TResult>
    {
        var totalValues = item.CountChildren<TSelf, TKey>();
        var results = GC.AllocateUninitializedArray<TResult>(totalValues);
        var index = 0;
        GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TSelector>(item, results, ref index);
        return results;
    }

    /// <summary>
    ///     Helper method to populate child items recursively.
    /// </summary>
    /// <param name="item">The current node with keyed children.</param>
    /// <param name="buffer">
    ///     The span to fill with child items.
    ///     Should be at least as big as the count of children.
    /// </param>
    /// <param name="index">The current index in the array.</param>
    [ExcludeFromCodeCoverage] // Wrapper
    public static void GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TSelector>(this KeyedBox<TKey, TSelf> item,
        Span<TResult> buffer, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<TSelf, TResult>
        => item.Item.GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TSelector>(buffer, ref index);

    /// <summary>
    ///     Helper method to populate child items recursively.
    /// </summary>
    /// <param name="item">The current node with keyed children.</param>
    /// <param name="buffer">
    ///     The span to fill with child items.
    ///     Should be at least as big as the count of children.
    /// </param>
    /// <param name="index">The current index in the array.</param>
    public static void GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TSelector>(this TSelf item, Span<TResult> buffer, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TSelector : struct, ISelector<TSelf, TResult>
    {
        // Populate breadth first. Improved cache locality helps here.
        foreach (var childPair in item.Children)
            buffer.DangerousGetReferenceAt(index++) = TSelector.Select(childPair.Value.Item);

        foreach (var childPair in item.Children)
            GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TSelector>(childPair.Value.Item, buffer, ref index);
    }

    /// <summary>
    ///     Recursively returns all the child items of this node that match the given filter, transformed by the selector.
    /// </summary>
    /// <param name="item">The boxed node with keyed children whose child items to obtain.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An array of all the transformed child items of this node that match the filter.</returns>
    public static TResult[] GetChildrenRecursive<TSelf, TKey, TResult, TFilter, TSelector>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
        where TSelector : struct, ISelector<TSelf, TResult>
        => item.Item.GetChildrenRecursive<TSelf, TKey, TResult, TFilter, TSelector>();

    /// <summary>
    ///     Recursively returns all the child items of this node that match the given filter, transformed by the selector.
    /// </summary>
    /// <param name="item">The node with keyed children whose child items to obtain.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <typeparam name="TSelector">The type of the selector.</typeparam>
    /// <returns>An array of all the transformed child items of this node that match the filter.</returns>
    public static TResult[] GetChildrenRecursive<TSelf, TKey, TResult, TFilter, TSelector>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
        where TSelector : struct, ISelector<TSelf, TResult>
    {
        var totalChildren = item.CountChildren<TSelf, TKey, TFilter>();
        var results = GC.AllocateUninitializedArray<TResult>(totalChildren);
        var index = 0;
        item.GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TFilter, TSelector>(results, ref index);
        return results;
    }

    /// <summary>
    ///     Helper method to populate transformed child items recursively, filtered by the given filter.
    /// </summary>
    /// <param name="item">The current node with keyed children.</param>
    /// <param name="buffer">The span to fill with transformed child items.</param>
    /// <param name="index">The current index in the span.</param>
    public static void GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TFilter, TSelector>(this KeyedBox<TKey, TSelf> item,
        Span<TResult> buffer, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
        where TSelector : struct, ISelector<TSelf, TResult>
        => item.Item.GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TFilter, TSelector>(buffer, ref index);

    /// <summary>
    ///     Helper method to populate transformed child items recursively, filtered by the given filter.
    /// </summary>
    /// <param name="item">The current node with keyed children.</param>
    /// <param name="buffer">The span to fill with transformed child items.</param>
    /// <param name="index">The current index in the span.</param>
    public static void GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TFilter, TSelector>(this TSelf item, Span<TResult> buffer, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
        where TSelector : struct, ISelector<TSelf, TResult>
    {
        foreach (var kvp in item.Children)
        {
            var child = kvp.Value.Item;
            if (TFilter.Match(child))
                buffer.DangerousGetReferenceAt(index++) = TSelector.Select(child);

            GetChildrenRecursiveUnsafe<TSelf, TKey, TResult, TFilter, TSelector>(child, buffer, ref index);
        }
    }

    /// <summary>
    ///     Recursively returns all the children of this node.
    /// </summary>
    /// <param name="item">The node whose children to obtain.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An array of all the children of this node.</returns>
    public static KeyedBox<TKey, TSelf>[] GetChildrenRecursive<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull => item.Item.GetChildrenRecursive<TSelf, TKey>();

    /// <summary>
    ///     Recursively returns all the children of this node.
    /// </summary>
    /// <param name="item">The node whose children to obtain.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <returns>An array of all the children of this node.</returns>
    public static KeyedBox<TKey, TSelf>[] GetChildrenRecursive<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        var totalChildren = item.CountChildren<TSelf, TKey>();
        var children = GC.AllocateUninitializedArray<KeyedBox<TKey, TSelf>>(totalChildren);
        var index = 0;
        GetChildrenRecursiveUnsafe<TSelf, TKey>(item, children, ref index);
        return children;
    }

    /// <summary>
    ///     Recursively returns all the children of this node (unsafe / no bounds checks).
    /// </summary>
    /// <param name="item">The current node.</param>
    /// <param name="childrenSpan">
    ///     The span representing the array to fill with children.
    ///     Should be at least as long as value returned by <see cref="CountChildren{TSelf,TKey}(TSelf)"/>
    /// </param>
    /// <param name="index">The current index in the span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetChildrenRecursiveUnsafe<TSelf, TKey>(TSelf item, Span<KeyedBox<TKey, TSelf>> childrenSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        foreach (var child in item.Children)
        {
            childrenSpan.DangerousGetReferenceAt(index++) = child.Value;
            GetChildrenRecursiveUnsafe(child.Value.Item, childrenSpan, ref index);
        }
    }

    /// <summary>
    ///     Recursively returns all the children of this node that match the given filter.
    /// </summary>
    /// <param name="item">The node whose children to obtain.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <returns>An array of all the children of this node that match the filter.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static KeyedBox<TKey, TSelf>[] GetChildrenRecursive<TSelf, TKey, TFilter>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
        => item.Item.GetChildrenRecursive<TSelf, TKey, TFilter>();

    /// <summary>
    ///     Recursively returns all the children of this node that match the given filter.
    /// </summary>
    /// <param name="item">The node whose children to obtain.</param>
    /// <typeparam name="TKey">The type of key used in the tree.</typeparam>
    /// <typeparam name="TSelf">The type of child node.</typeparam>
    /// <typeparam name="TFilter">The type of the filter.</typeparam>
    /// <returns>An array of all the children of this node that match the filter.</returns>
    public static KeyedBox<TKey, TSelf>[] GetChildrenRecursive<TSelf, TKey, TFilter>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
    {
        var totalChildren = item.CountChildren<TSelf, TKey, TFilter>();
        var children = GC.AllocateUninitializedArray<KeyedBox<TKey, TSelf>>(totalChildren);
        var index = 0;
        GetChildrenRecursiveUnsafe<TSelf, TKey, TFilter>(item, children, ref index);
        return children;
    }

    /// <summary>
    ///     Recursively returns all the children of this node (unsafe / no bounds checks).
    /// </summary>
    /// <param name="item">The current node.</param>
    /// <param name="childrenSpan">
    ///     The span representing the array to fill with children.
    ///     Should be at least as long as the value returned by <see cref="CountChildren{TSelf,TKey,TFilter}(TSelf)"/>
    /// </param>
    /// <param name="index">The current index in the span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetChildrenRecursiveUnsafe<TSelf, TKey, TFilter>(this TSelf item, Span<KeyedBox<TKey, TSelf>> childrenSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        where TFilter : struct, IFilter<TSelf>
    {
        foreach (var kvp in item.Children)
        {
            if (TFilter.Match(kvp.Value.Item))
                childrenSpan.DangerousGetReferenceAt(index++) = kvp.Value;

            GetChildrenRecursiveUnsafe<TSelf, TKey, TFilter>(kvp.Value.Item, childrenSpan, ref index);
        }
    }

    /// <summary>
    ///     Counts the number of leaf nodes (nodes with no children) of the current node.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <param name="item">The boxed node whose leaf nodes are to be counted.</param>
    /// <returns>The count of leaf nodes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountLeaves<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Item.CountLeaves<TSelf, TKey>();

    /// <summary>
    ///     Counts the number of leaf nodes (nodes with no children) under the current node.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <param name="item">The node whose leaf nodes are to be counted.</param>
    /// <returns>The total count of leaf nodes under the current node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountLeaves<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        var leafCount = 0;
        CountLeavesRecursive<TSelf, TKey>(item, ref leafCount);
        return leafCount;
    }

    private static void CountLeavesRecursive<TSelf, TKey>(TSelf item, ref int leafCount)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        foreach (var pair in item.Children)
        {
            if (pair.Value.IsLeaf())
                leafCount++;
            else
                CountLeavesRecursive<TSelf, TKey>(pair.Value.Item, ref leafCount);
        }
    }

    /// <summary>
    ///     Recursively finds and returns all leaf nodes under the current node.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <param name="item">The boxed node whose leaf nodes are to be found.</param>
    /// <returns>An array of all leaf nodes under the current node.</returns>
    public static KeyedBox<TKey, TSelf>[] GetLeaves<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Item.GetLeaves<TSelf, TKey>();

    /// <summary>
    ///     Recursively finds and returns all leaf nodes under the current node.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <param name="item">The node whose leaf nodes are to be found.</param>
    /// <returns>An array of all leaf nodes under the current node.</returns>
    public static KeyedBox<TKey, TSelf>[] GetLeaves<TSelf, TKey>(this TSelf item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        var totalLeaves = item.CountLeaves<TSelf, TKey>();
        var leaves = GC.AllocateUninitializedArray<KeyedBox<TKey, TSelf>>(totalLeaves);
        var index = 0;
        GetLeavesUnsafe<TSelf, TKey>(item, leaves, ref index);
        return leaves;
    }

    /// <summary>
    ///     Helper method to populate leaf nodes recursively without bounds checking.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <param name="item">The current node to find leaf nodes from.</param>
    /// <param name="leavesSpan">The span to fill with leaf nodes.</param>
    /// <param name="index">Current index in the span, used internally for recursion.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetLeavesUnsafe<TSelf, TKey>(this TSelf item, Span<KeyedBox<TKey, TSelf>> leavesSpan, ref int index)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
    {
        foreach (var pair in item.Children)
        {
            if (pair.Value.IsLeaf())
                leavesSpan.DangerousGetReferenceAt(index++) = pair.Value;
            else
                GetLeavesUnsafe(pair.Value.Item, leavesSpan, ref index);
        }
    }

    /// <summary>
    ///     Retrieves the dictionary containing all the keyed children of the current node.
    /// </summary>
    /// <param name="item">The keyed box containing the node whose children are to be retrieved.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>A dictionary of the children keyed by TKey.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static Dictionary<TKey, KeyedBox<TKey, TSelf>> Children<TSelf, TKey>(this KeyedBox<TKey, TSelf> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Item.Children;

    /// <summary>
    ///     Retrieves the dictionary containing all the keyed children of the current node.
    /// </summary>
    /// <param name="item">The keyed box containing the node whose children are to be retrieved.</param>
    /// <typeparam name="TKey">The type of key used to identify children.</typeparam>
    /// <typeparam name="TSelf">The type of the child node.</typeparam>
    /// <returns>A dictionary of the children keyed by TKey.</returns>
    [ExcludeFromCodeCoverage] // Wrapper
    public static Dictionary<TKey, KeyedBox<TKey, TSelf>> Children<TSelf, TKey>(this KeyValuePair<TKey, KeyedBox<TKey, TSelf>> item)
        where TSelf : struct, IHaveBoxedChildrenWithKey<TKey, TSelf>
        where TKey : notnull
        => item.Value.Item.Children;
}
